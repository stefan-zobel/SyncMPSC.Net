/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using librocks.Net;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SyncMPSC.Ipc.Sockets;

public sealed class MessagingLibServerServiceImpl /*: IMessagingLibServerService */
{
    private static readonly ILogger<MessagingLibServerServiceImpl> LOGGER = LogManager.GetLogger<MessagingLibServerServiceImpl>();

    private static string _rocksDBPath = "./rocksdb_database";
    private string _version = "?.?.?";
    private QueueManager? _km;

    private readonly ConcurrentDictionary<string, QueueConfig> _queueConfigs = new();
    private readonly ConcurrentDictionary<string, QueueServerThread> _queueServerThreads = new();

    private record QueueConfig(
        string Id,
        int ReadPort,
        int WritePort,
        HashSet<string>? AdditionalWriteQueues,
        string? ListenerIP)
    {
        public IMessagePassthrough? Passthrough { get; set; }
    }

    private class QueueServerThread(string id, IQueueTransmitter transmitter, ServerEndpoint? sep)
    {
        public string Id { get; } = id;
        public IQueueTransmitter Transmitter { get; } = transmitter;
        public ServerEndpoint? Sep { get; } = sep;
    }

    public string Name => nameof(MessagingLibServerServiceImpl); // nameof(IMessagingLibServerService);

    public QueueManager? GetQueueManager() => _km;

    public void Init()
    {
        ToggleRocksDBFsync();
        StopQueueServerThreads();

        _rocksDBPath = PropertyService.GetProperty(MessagingConstants.ROCKSDB_PATH) ?? _rocksDBPath;

        if (_km != null && _km.IsOpen)
        {
            _km.Dispose();
        }

        // Initialize QueueManager
        _km = new QueueManager(_rocksDBPath);
        _version = _km.GetRocksDBVersion();

        LOGGER.LogInformation("Using RocksDB version {Version}", _version);
        LOGGER.LogInformation("Flushing and syncing WALs");
        _km.SyncWal();

        LOGGER.LogInformation("Compacting database");
        _km.CompactAll();

        ReadQueueConfigs();
        StartQueueServerThreads();
    }

    public void Shutdown()
    {
        StopQueueServerThreads();
        _km?.Dispose();
        _queueConfigs.Clear();
    }

    private void ToggleRocksDBFsync()
    {
        bool isFsyncDisabled = PropertyService.GetPropertyAsBoolean(MessagingConstants.ROCKSDB_FSYNC_DISABLED);
        // In .NET, environment variables or AppContext switches are used instead of System.setProperty
        string doOccasionalWalSync = isFsyncDisabled ? "false" : "true";
        Environment.SetEnvironmentVariable("ROCKSDB_OCCASIONAL_WAL_SYNC", doOccasionalWalSync);
        LOGGER.LogInformation("Occasional WAL syncing is {Status}", doOccasionalWalSync);
    }

    private void ReadQueueConfigs()
    {
        _queueConfigs.Clear();
        const string? optionalListenerIP = null; // TODO - where does this come from?

        var queues = PropertyService.GetPropertyTable(MessagingConstants.PROPERTY_QUEUE);
        foreach (var queue in queues)
        {
            string? queueId = queue.GetValueOrDefault(MessagingConstants.QUEUE_ID);
            if (int.TryParse(queue.GetValueOrDefault(MessagingConstants.QUEUE_READ_PORT), out int readPort) &&
                int.TryParse(queue.GetValueOrDefault(MessagingConstants.QUEUE_WRITE_PORT), out int writePort) &&
                !string.IsNullOrWhiteSpace(queueId) && IsValidPort(readPort) && IsValidPort(writePort))
            {
                HashSet<string>? additionalQueues = null;
                string? copyIntoList = queue.GetValueOrDefault(MessagingConstants.QUEUE_COPY_INTO_QUEUES);
                if (!string.IsNullOrEmpty(copyIntoList))
                {
                    additionalQueues = copyIntoList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
                }

                var qConfig = new QueueConfig(queueId, readPort, writePort, additionalQueues, optionalListenerIP);
                _queueConfigs[queueId] = qConfig;
            }
        }

        // Read Whitelists
        var whitelists = PropertyService.GetPropertyTable(MessagingConstants.PROPERTY_WHITELIST);
        foreach (var wl in whitelists)
        {
            string? queueId = wl.GetValueOrDefault(MessagingConstants.WHITELIST_QUEUE_ID);
            string? passthrough = wl.GetValueOrDefault(MessagingConstants.WHITELIST_PASSTHROUGH);

            if (!string.IsNullOrWhiteSpace(queueId) && !string.IsNullOrWhiteSpace(passthrough) && _queueConfigs.TryGetValue(queueId, out var cfg))
            {
                // TODO
                /*
                var typeTags = passthrough.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (typeTags.Count > 0)
                {
                    // Logic assumes the .NET Whitelist implementation implements IMessagePassthrough
                    var whitelist = new Whitelist(typeTags);
                    if (whitelist.Any())
                    {
                        cfg.Passthrough = whitelist;
                    }
                }
                */
                if ("*" == passthrough.Trim())
                {
                    var whitelist = new AnyMessagePassthrough();
                    cfg.Passthrough = whitelist;
                }
            }
        }
    }

    private void StartQueueServerThreads()
    {
        if (_km == null) return;

        foreach (var qCfg in _queueConfigs.Values)
        {
            var transmitter = new QueueTransmitterImpl(qCfg.Id, qCfg.ReadPort, qCfg.ListenerIP, _km.Get(qCfg.Id));
            var sep = new ServerEndpoint(qCfg.Id, qCfg.WritePort);

            Dictionary<string, IMessagePassthrough>? passthroughsForAdditionalQueues = null;
            if (qCfg.AdditionalWriteQueues?.Count > 0)
            {
                passthroughsForAdditionalQueues = new Dictionary<string, IMessagePassthrough>(4);
                foreach (string copyIntoQueue in qCfg.AdditionalWriteQueues)
                {
                    if (_queueConfigs.TryGetValue(copyIntoQueue, out var copyToConfig) && copyToConfig.Passthrough != null)
                    {
                        passthroughsForAdditionalQueues[copyIntoQueue] = copyToConfig.Passthrough;
                    }
                }
            }

            sep.RegisterMessageHandler(new QueueProducer(qCfg.Id, _km.Get(qCfg.Id), qCfg.AdditionalWriteQueues, passthroughsForAdditionalQueues, _km));

            var threadEntry = new QueueServerThread(qCfg.Id, transmitter, sep);
            _queueServerThreads[qCfg.Id] = threadEntry;

            LOGGER.LogInformation("Starting QueueServerThread for {Id}", threadEntry.Id);
            transmitter.StartAsync();
            sep.StartAsync();
        }
    }

    private void StopQueueServerThreads()
    {
        foreach (var entry in _queueServerThreads.Values)
        {
            LOGGER.LogInformation("Stopping QueueServerThread for {Id}", entry.Id);
            entry.Transmitter.Stop();
            entry.Sep?.Stop();
        }
        _queueServerThreads.Clear();
    }

    private static bool IsValidPort(int n) => n is >= 1 and <= 65535;
}
