/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

internal static class MessagingConstants
{
    internal const int CONNECT_TIMEOUT_MILLIS_DEFAULT = 3_000;
    internal const int SENDER_READ_TIMEOUT_MILLIS_DEFAULT = 500;
    internal const string CONNECT_TIMEOUT_MILLIS = "connectTimeoutMillis";
    internal const string SENDER_READ_TIMEOUT_MILLIS = "senderReadTimeoutMillis";
    internal const string PROPERTY_DEFAULT_QUEUE_ID = "defaultqueue.id";
    internal const string PROPERTY_DEFAULT_QUEUE_WRITE_PORT = "defaultqueue.port.write";
    internal const string PROPERTY_DEFAULT_QUEUE_READ_PORT = "defaultqueue.port.read";
    internal const string PROPERTY_LONG_WAITS_LOGGING_ENABLED = "longWaitsLoggingEnabled";
    /** Optional: IP or host name under which the server is listening */
    internal const string PROPERTY_SERVER = "server";
    internal const string PROPERTY_IS_CLIENT_ENABLED = "enabled";
    internal const string PROPERTY_IS_CLIENT_SENDING_ENABLED = "sending.enabled";
    internal const string PROPERTY_IS_CLIENT_RECEIVING_ENABLED = "receiving.enabled";
    internal const string ROCKSDB_PATH = "rocksdb_path";
    internal const string ROCKSDB_FSYNC_DISABLED = "rocksdb.fsync.disabled";
    internal const string PROPERTY_QUEUE = "messaging.queue";
    internal const string QUEUE_ID = "id";
    internal const string QUEUE_READ_PORT = "portRead";
    internal const string QUEUE_WRITE_PORT = "portWrite";
    internal const string QUEUE_COPY_INTO_QUEUES = "copyIntoQueues";
    internal const string PROPERTY_WHITELIST = PROPERTY_QUEUE + ".copy.into.whitelist";
    internal const string WHITELIST_QUEUE_ID = "id";
    internal const string WHITELIST_PASSTHROUGH = "passthrough";
    internal const string LOG_PATH = "logging.file.path";
    internal const string LOG_LEVEL = "logging.file.level";
    internal const string LOG_TEMPLATE = "logging.file.template";
}
