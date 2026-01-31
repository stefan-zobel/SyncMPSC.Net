/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SyncMPSC.Ipc.Sockets;

public sealed class MessagingLibClientServiceImpl /*: IMessagingLibClientService */
{
    private static readonly ILogger<MessagingLibClientServiceImpl> LOGGER = LogManager.GetLogger<MessagingLibClientServiceImpl>();

    private const string DEFAULT_HOST = "localhost";

    private int _receiverConnectTimeoutMillis = MessagingConstants.CONNECT_TIMEOUT_MILLIS_DEFAULT;
    private int _senderReadTimeoutMillis = MessagingConstants.SENDER_READ_TIMEOUT_MILLIS_DEFAULT;
    private string? _defaultQueueId = null;
    private int _defaultQueueWritePort = -1;
    private int _defaultQueueReadPort = -1;

    private readonly ConcurrentDictionary<QueueAddress, IQueueSender> _senders = new();

    private record struct QueueAddress(string ServerHost, int ServerPort)
    {
        public string ServerHost { get; init; } = ServerHost.Trim().ToLowerInvariant();
    }

    public string Name => nameof(MessagingLibClientServiceImpl); //nameof(IMessagingLibClientService);

    public void Init()
    {
        _defaultQueueId = null;
        _defaultQueueWritePort = -1;
        _defaultQueueReadPort = -1;

        _receiverConnectTimeoutMillis = PropertyService.GetPropertyAsIntOrDefault(
            MessagingConstants.CONNECT_TIMEOUT_MILLIS, MessagingConstants.CONNECT_TIMEOUT_MILLIS_DEFAULT);
        _senderReadTimeoutMillis = PropertyService.GetPropertyAsIntOrDefault(
            MessagingConstants.SENDER_READ_TIMEOUT_MILLIS, MessagingConstants.SENDER_READ_TIMEOUT_MILLIS_DEFAULT);

        string? defaultClientQueueId = PropertyService.GetPropertyOrDefault(MessagingConstants.PROPERTY_DEFAULT_QUEUE_ID, "default");

        if (!string.IsNullOrWhiteSpace(defaultClientQueueId))
        {
            int writePort = PropertyService.GetPropertyAsIntOrDefault(MessagingConstants.PROPERTY_DEFAULT_QUEUE_WRITE_PORT, -1);
            int readPort = PropertyService.GetPropertyAsIntOrDefault(MessagingConstants.PROPERTY_DEFAULT_QUEUE_READ_PORT, -1);

            if (IsValidPort(writePort))
            {
                _defaultQueueId = defaultClientQueueId.Trim();
                _defaultQueueWritePort = writePort;
            }
            if (IsValidPort(readPort))
            {
                _defaultQueueId = defaultClientQueueId.Trim();
                _defaultQueueReadPort = readPort;
            }
        }
    }

    public void Shutdown()
    {
        foreach (var sender in _senders.Values)
        {
            sender.Shutdown();
            LOGGER.LogInformation("Shutdown sender: {SenderId}", sender.Id);
        }
        _senders.Clear();
    }

    public IQueueSender RegisterQueueSender(string id, string serverHost, int serverPort)
    {
        bool logLongWaits = PropertyService.GetPropertyAsBoolean(MessagingConstants.PROPERTY_LONG_WAITS_LOGGING_ENABLED);
        var address = new QueueAddress(serverHost, serverPort);

        var newSender = new QueueSenderImpl(id, serverHost, serverPort, _senderReadTimeoutMillis, logLongWaits);

        // ConcurrentDictionary.GetOrAdd is the .NET equivalent of putIfAbsent
        return _senders.GetOrAdd(address, newSender);
    }

    public IQueueReceiver CreateQueueReceiver(string id, string serverHost, int serverPort, IMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        // Anonymous implementation of MessageAndReplyHandler via local class/function
        var adapter = new AnonymousReplyHandler(handler, id);
        return new QueueReceiverImpl(id, serverHost, serverPort, _receiverConnectTimeoutMillis, adapter);
    }

    public IQueueSender GetDefaultQueueSender()
    {
        if (!string.IsNullOrWhiteSpace(_defaultQueueId) && IsValidPort(_defaultQueueWritePort))
        {
            string serverHost = PropertyService.GetProperty(MessagingConstants.PROPERTY_SERVER)?.Trim() ?? DEFAULT_HOST;

            var address = new QueueAddress(serverHost, _defaultQueueWritePort);

            if (_senders.TryGetValue(address, out var qs))
            {
                if (_defaultQueueId.Equals(qs.Id)) return qs;

                throw new Exception($"QueueSender mismatch: Expected {_defaultQueueId}, found {qs.Id}");
            }

            return RegisterQueueSender(_defaultQueueId, serverHost, _defaultQueueWritePort);
        }
        throw new InvalidOperationException("Missing QueueSender configuration");
    }

    public IQueueReceiver CreateDefaultQueueReceiver(IMessageHandler handler)
    {
        if (!string.IsNullOrWhiteSpace(_defaultQueueId) && IsValidPort(_defaultQueueReadPort))
        {
            string serverHost = PropertyService.GetProperty(MessagingConstants.PROPERTY_SERVER)?.Trim() ?? DEFAULT_HOST;
            if (string.IsNullOrEmpty(serverHost)) serverHost = DEFAULT_HOST;

            return CreateQueueReceiver(_defaultQueueId, serverHost, _defaultQueueReadPort, handler);
        }
        throw new InvalidOperationException("Missing QueueReceiver configuration");
    }

    public static bool IsLocalQueueStorageSendingEnabled => 
        PropertyService.GetPropertyAsBoolean(MessagingConstants.PROPERTY_IS_CLIENT_ENABLED) &&
        PropertyService.GetPropertyAsBoolean(MessagingConstants.PROPERTY_IS_CLIENT_SENDING_ENABLED);

    public static bool IsLocalQueueStorageReceivingEnabled =>
        PropertyService.GetPropertyAsBoolean(MessagingConstants.PROPERTY_IS_CLIENT_ENABLED) &&
        PropertyService.GetPropertyAsBoolean(MessagingConstants.PROPERTY_IS_CLIENT_RECEIVING_ENABLED);

    private static bool IsValidPort(int n) => n is >= 1 and <= 65535;

    // Helper class for the Receiver adapter
    private class AnonymousReplyHandler(IMessageHandler msgHandler, string id) : IMessageAndReplyHandler
    {
        public void OnNext(byte[] message, Stream reply)
        {
            try
            {
                reply.Write(Protocol.REPLY_OK, 0, Protocol.REPLY_OK.Length);
                reply.Flush();
                msgHandler.OnNext(message);
            }
            catch (Exception ex)
            {
                LOGGER.LogWarning(ex, "Error processing message in receiver {Id}", id);
            }
        }
    }
}
