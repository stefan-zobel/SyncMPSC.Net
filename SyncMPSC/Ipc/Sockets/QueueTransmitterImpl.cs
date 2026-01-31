/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using librocks.Net;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SyncMPSC.Ipc.Sockets;

/// <summary>
/// Consumes messages from a persistent Queue and sends them out to a consumer via TCP.
/// </summary>
public sealed class QueueTransmitterImpl : IQueueTransmitter
{
    private static readonly ILogger<QueueTransmitterImpl> LOGGER = LogManager.GetLogger<QueueTransmitterImpl>();

    private const int DEFAULT_BACKLOG = 50;
    private const int DEFAULT_ACCEPT_TIMEOUT_MILLIS = 500;

    private readonly int _port;
    private readonly IPAddress? _bindAddress;
    private readonly Queue _readQueue;
    private readonly string _id;

    private int _isRunning = 0; // 0 = false, 1 = true
    private TcpListener? _srvSocket;
    private Thread? _asyncStarter;
    private TcpClient? _client;

    public QueueTransmitterImpl(string id, int port, string? ipAddressString, Queue kueue)
    {
        _port = port;
        _readQueue = kueue;
        _id = id;

        if (!string.IsNullOrEmpty(ipAddressString))
        {
            try
            {
                _bindAddress = IPAddress.Parse(ipAddressString);
            }
            catch (Exception e)
            {
                LOGGER.LogError(e, "Error parsing IP address {IP}", ipAddressString);
                throw new WireException(e);
            }
        }
    }

    public string Id => _id;

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public void StartAsync()
    {
        _asyncStarter = new Thread(() =>
        {
            try
            {
                Start();
            }
            catch (Exception e)
            {
                if (e is not WireException) throw new WireException(e);
                throw;
            }
        })
        {
            Name = $"{nameof(QueueTransmitterImpl)}-{_port}-Thread-{GetHashCode()}",
            IsBackground = true
        };
        _asyncStarter.Start();
    }

    public long Stop()
    {
        Interlocked.Exchange(ref _isRunning, 0);
        long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_asyncStarter != null)
        {
            try
            {
                // No direct "interrupt" in .NET; TcpListener.Stop() will break the loop
                _asyncStarter.Join(DEFAULT_ACCEPT_TIMEOUT_MILLIS);
            }
            catch { /* Ignored */ }
            _asyncStarter = null;
        }

        _srvSocket?.Stop();
        _srvSocket = null;

        _client?.Close();
        _client = null;

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;
    }

    internal void Start()
    {
        _srvSocket = new TcpListener(_bindAddress ?? IPAddress.Any, _port);
        _srvSocket.ExclusiveAddressUse = false; // Equivalent to setReuseAddress(true)

        // Start with the specified backlog
        _srvSocket.Start(DEFAULT_BACKLOG);

        Interlocked.Exchange(ref _isRunning, 1);
        LOGGER.LogInformation("Server started on port {Port}", _port);

        Transmit();
    }

    private void Transmit()
    {
        try
        {
            LOGGER.LogInformation("Starting transmit loop on port {Port}", _port);
            while (IsRunning)
            {
                _client = AcceptNextConnection();
                if (_client != null)
                {
                    LOGGER.LogInformation("Client connected to port {Port} from {Remote}",
                        _port, _client.Client.RemoteEndPoint);

                    var consumer = new QueueConsumer(_client, this);
                    QueueMsgConsumer acceptor = new QueueMsgConsumer(consumer.Accept);
                    try
                    {
                        while (IsRunning && ConsumerSocketIsReachable())
                        {
                            ConsumeMessage(acceptor);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Thread.CurrentThread.Interrupt();
                    }
                    catch (Exception ex)
                    {
                        LOGGER.LogWarning(ex, "Socket exception; listening for new connection.");
                    }
                    finally
                    {
                        _client.Close();
                    }
                }
            }
        }
        catch (SocketException ex)
        {
            LOGGER.LogWarning(ex, "Server socket closed.");
        }
    }

    private TcpClient? AcceptNextConnection()
    {
        while (IsRunning)
        {
            try
            {
                // Pending() allows non-blocking check to see if we should continue IsRunning loop
                if (_srvSocket != null && _srvSocket.Pending())
                {
                    return _srvSocket.AcceptTcpClient();
                }
                Thread.Sleep(10);
            }
            catch (SocketException) { /* Ignore */ }
        }
        return null;
    }

    private bool ConsumerSocketIsReachable()
    {
        if (_client is { Connected: true })
        {
            try
            {
                // determination of reachability by sending the ALIVE_BYTE
                _client.GetStream().Write(Protocol.ALIVE_BYTE, 0, Protocol.ALIVE_BYTE.Length);
                _client.GetStream().Flush();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
        return false;
    }

    private bool ConsumeMessage(QueueMsgConsumer consumer)
    {
        return _readQueue.TryAccept(consumer, TimeSpan.FromMilliseconds(64));
    }
}
