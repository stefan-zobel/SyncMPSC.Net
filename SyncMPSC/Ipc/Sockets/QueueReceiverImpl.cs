/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SyncMPSC.Ipc.Sockets;

public sealed class QueueReceiverImpl : IQueueReceiver
{
    private static readonly ILogger<QueueReceiverImpl> LOGGER = LogManager.GetLogger<QueueReceiverImpl>();

    private readonly IPEndPoint _serverAddress;
    private readonly int _connectTimeoutMillis;
    private readonly object _socketLock = new();
    private readonly byte[] _frameDelimiter = (byte[])Protocol.END_OF_REQUEST.Clone();
    private readonly string _id;
    private readonly IMessageAndReplyHandler _frameProcessor;

    private TcpClient? _clientSock;
    private Stream? _socketStream;
    private Thread? _asyncStarter;
    private volatile bool _isRunning;

    public QueueReceiverImpl(string id, string serverHost, int serverPort, int connectTimeoutMillis, IMessageAndReplyHandler frameProcessor)
    {
        _id = id;
        _serverAddress = NetworkHelper.ResolveEndPoint(serverHost, serverPort);
        _connectTimeoutMillis = connectTimeoutMillis;
        _frameProcessor = frameProcessor;
    }

    public string Id => _id;

    public IPEndPoint ServerAddress => _serverAddress;

    public bool IsRunning => _isRunning && IsSocketReady();

    public void Start()
    {
        if (!IsRunning)
        {
            if (Open())
            {
                StartAsyncInternal();
            }
        }
    }

    internal void StartAsyncInternal()
    {
        _asyncStarter = new Thread(() =>
        {
            try
            {
                InternalStart();
            }
            catch (Exception e)
            {
                LOGGER.LogInformation("Error on receiver thread for {Address}", _serverAddress);
                if (e is not WireException) throw new WireException(e);
                throw;
            }
        })
        {
            Name = $"{nameof(QueueReceiverImpl)}-Thread-{GetHashCode()}",
            IsBackground = true
        };
        _asyncStarter.Start();
    }

    private void InternalStart()
    {
        // Ensure connection is established (Open handles safety if already connected)
        Open();
        Listen();
    }

    private bool Open()
    {
        try
        {
            bool isNew = Connect();
            _isRunning = true;
            return isNew;
        }
        catch (IOException e)
        {
            throw new WireException(e);
        }
    }

    public long Stop()
    {
        _isRunning = false;
        long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Disconnect();

        if (_asyncStarter != null)
        {
            try
            {
                _asyncStarter.Join(500);
            }
            catch { /* Ignored */ }
            _asyncStarter = null;
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;
    }

    private bool IsSocketReady()
    {
        // Check if thread is interrupted and socket is alive
        return (Thread.CurrentThread.ThreadState & ThreadState.WaitSleepJoin) != ThreadState.WaitSleepJoin
            && _clientSock is { Connected: true } && _socketStream != null && _socketStream.CanRead;
    }

    private void Listen()
    {
        LOGGER.LogInformation("Started listening to {Address}", _serverAddress);
        try
        {
            byte[]? frame;
            while (_isRunning && IsSocketReady() && (frame = FrameDecoder.NextFrame(_socketStream!, _frameDelimiter)) != null)
            {
                if (!Protocol.IsAliveRequest(frame))
                {
                    _frameProcessor.OnNext(frame, _socketStream!);
                }
            }
            LOGGER.LogInformation("Stopped listening to {Address}", _serverAddress);
        }
        catch (Exception e)
        {
            if (e is OperationCanceledException || e is ThreadInterruptedException) return;
            throw new WireException(e);
        }
        finally
        {
            Disconnect();
        }
    }

    private bool IsConnected()
    {
        lock (_socketLock)
        {
            return _clientSock is { Connected: true } && _socketStream != null && _socketStream.CanRead;
        }
    }

    private bool Connect()
    {
        lock (_socketLock)
        {
            if (!IsConnected())
            {
                try
                {
                    _clientSock = new TcpClient();
                    // .NET Connect with timeout requires a Task-based or ManualResetEvent approach
                    var result = _clientSock.BeginConnect(_serverAddress.Address, _serverAddress.Port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(_connectTimeoutMillis);

                    if (!success) throw new SocketException((int)SocketError.TimedOut);

                    _clientSock.EndConnect(result);
                    _socketStream = _clientSock.GetStream();

                    LOGGER.LogInformation("Connected to {Address} on local port {Port}",
                        _serverAddress, ((IPEndPoint)_clientSock.Client.LocalEndPoint!).Port);
                    return true;
                }
                catch (Exception)
                {
                    Disconnect();
                    throw;
                }
            }
            return false;
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
}
