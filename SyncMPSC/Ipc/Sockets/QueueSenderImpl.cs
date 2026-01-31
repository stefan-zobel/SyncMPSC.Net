/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace SyncMPSC.Ipc.Sockets;

public sealed class QueueSenderImpl : Stream, IQueueSender
{
    private static readonly ILogger<QueueSenderImpl> LOGGER = LogManager.GetLogger<QueueSenderImpl>();

    private const int DEFAULT_READ_TIMEOUT_MILLIS = 500;
    private const int DEFAULT_CONNECT_TIMEOUT_MILLIS = 1_500;
    private const long DEFAULT_SERVER_REPLY_WARN_THRESHOLD_TICKS = 5_000L * TimeSpan.TicksPerMillisecond;

    private readonly IPEndPoint _receiverAddress;
    private readonly int _connectTimeoutMillis;
    private readonly int _readTimeoutMillis;
    private readonly byte[] _frameDelimiter;
    private readonly bool _logLongWaitsWarning;
    private readonly string _id;

    private TcpClient? _clientSock;
    private NetworkStream? _socketStream;
    private int _sendBufferSize = -1;
    private volatile bool _isRunning = true;

    private readonly object _socketLock = new();
    // SemaphoreSlim(1,1) is the standard .NET replacement for ReentrantLock
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public QueueSenderImpl(string id, string receiverHost, int receiverPort)
        : this(id, receiverHost, receiverPort, DEFAULT_CONNECT_TIMEOUT_MILLIS, Protocol.END_OF_REQUEST, DEFAULT_READ_TIMEOUT_MILLIS, false) { }

    public QueueSenderImpl(string id, string receiverHost, int receiverPort, int readTimeoutMillis, bool logLongWaitsWarning)
        : this(id, receiverHost, receiverPort, DEFAULT_CONNECT_TIMEOUT_MILLIS, Protocol.END_OF_REQUEST, readTimeoutMillis, logLongWaitsWarning) { }

    public QueueSenderImpl(string id, string receiverHost, int receiverPort, byte[] delimiter)
        : this(id, receiverHost, receiverPort, DEFAULT_CONNECT_TIMEOUT_MILLIS, delimiter, DEFAULT_READ_TIMEOUT_MILLIS, false) { }

    public QueueSenderImpl(string id, string receiverHost, int receiverPort, int connectTimeoutMillis, byte[] delimiter,
        int readTimeoutMillis, bool logLongWaitsWarning)
    {
        if (delimiter == null || delimiter.Length == 0)
            throw new ArgumentException("delimiter null or empty");

        _receiverAddress = NetworkHelper.ResolveEndPoint(receiverHost, receiverPort);
        _connectTimeoutMillis = connectTimeoutMillis;
        _readTimeoutMillis = readTimeoutMillis;
        _frameDelimiter = (byte[])delimiter.Clone();
        _logLongWaitsWarning = logLongWaitsWarning;
        _id = id;
    }

    private void Connect()
    {
        if (!_isRunning) return;

        lock (_socketLock)
        {
            if (!IsConnected())
            {
                try
                {
                    _clientSock = new TcpClient();
                    _clientSock.ReceiveTimeout = _readTimeoutMillis;

                    // Connect with timeout
                    var result = _clientSock.BeginConnect(_receiverAddress.Address, _receiverAddress.Port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(_connectTimeoutMillis))
                    {
                        throw new TimeoutException("Connection timed out");
                    }
                    _clientSock.EndConnect(result);

                    if (_sendBufferSize < 0)
                        _sendBufferSize = _clientSock.SendBufferSize;

                    _socketStream = _clientSock.GetStream();
                }
                catch (Exception e)
                {
                    Disconnect();
                    if (e is IOException) throw;
                    throw new IOException("Failed to connect", e);
                }
            }
        }
    }

    private void Disconnect()
    {
        lock (_socketLock)
        {
            _socketStream?.Dispose();
            _socketStream = null;
            _clientSock?.Dispose();
            _clientSock = null;
        }
    }

    private bool IsConnected()
    {
        if (!_isRunning) return false;
        lock (_socketLock)
        {
            return _clientSock is { Connected: true } && _socketStream != null;
        }
    }

    public string Id => _id;

    public bool IsInterProcessComm => true;

    public void Shutdown()
    {
        _isRunning = false;
        Dispose();
    }

    public bool SendMessage(byte[] message)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        _sendLock.Wait();
        try
        {
            Write(message, 0, message.Length);
            byte[]? reply = Submit();
            return Protocol.IsOkReply(reply);
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Error sending message in sender {Id}", _id);
            return false;
        }
        finally
        {
            _sendLock.Release();
            // TODO: SendTimes logic here
        }
    }

    public byte[]? Submit()
    {
        if (!IsConnected()) throw new IOException("not connected");

        try
        {
            _socketStream!.Write(_frameDelimiter, 0, _frameDelimiter.Length);
            _socketStream.Flush();

            byte[]? serverReply;
            if (_logLongWaitsWarning)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                serverReply = FrameDecoder.NextFrame(_socketStream, _frameDelimiter);
                var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);

                if (elapsed.Ticks >= DEFAULT_SERVER_REPLY_WARN_THRESHOLD_TICKS)
                {
                    LOGGER.LogWarning("Server reply threshold exceeded: {Elapsed}ms", elapsed.TotalMilliseconds);
                }
            }
            else
            {
                serverReply = FrameDecoder.NextFrame(_socketStream, _frameDelimiter);
            }
            return serverReply;
        }
        catch (Exception)
        {
            Disconnect();
            throw;
        }
    }

    /// <summary>
    /// Reads all data from the provided input stream and writes it to the socket.
    /// </summary>
    public void Write(Stream inputStream)
    {
        lock (_socketLock)
        {
            if (!IsConnected())
            {
                Connect();
            }
        }

        try
        {
            // Use the buffer size determined during connection
            byte[] buffer = new byte[_sendBufferSize > 0 ? _sendBufferSize : 4096];
            int bytesRead;

            // .NET Stream.Read returns 0 at the end of the stream, unlike Java's -1
            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                _socketStream!.Write(buffer, 0, bytesRead);
            }
        }
        catch (Exception e)
        {
            Disconnect();
            if (e is IOException) throw;
            throw new IOException("Error piping input stream to socket", e);
        }
    }

    // Stream Implementation Requirements
    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_socketLock)
        {
            if (!IsConnected()) Connect();
        }

        try
        {
            _socketStream!.Write(buffer, offset, count);
        }
        catch (Exception)
        {
            Disconnect();
            throw;
        }
    }

    public override void WriteByte(byte value) => Write([value], 0, 1);

    // Required Stream Overrides
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _socketStream?.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disconnect();
//            _sendLock.Dispose(); // TODO
        }
        base.Dispose(disposing);
    }

    public override int GetHashCode() => _receiverAddress.GetHashCode();

    public override bool Equals(object? obj) =>
        obj is QueueSenderImpl other && _receiverAddress.Equals(other._receiverAddress);
}
