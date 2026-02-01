/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC;

public static class MessagingConstants
{
    internal const int CONNECT_TIMEOUT_MILLIS_DEFAULT = 3_000;
    internal const int SENDER_READ_TIMEOUT_MILLIS_DEFAULT = 500;
    internal const string CONNECT_TIMEOUT_MILLIS = "connectTimeoutMillis";
    internal const string SENDER_READ_TIMEOUT_MILLIS = "senderReadTimeoutMillis";
    public const string PROPERTY_DEFAULT_QUEUE_ID = "defaultqueue.id";
    public const string PROPERTY_DEFAULT_QUEUE_WRITE_PORT = "defaultqueue.port.write";
    public const string PROPERTY_DEFAULT_QUEUE_READ_PORT = "defaultqueue.port.read";
    public const string PROPERTY_LONG_WAITS_LOGGING_ENABLED = "longWaitsLoggingEnabled";
    /** Optional: IP or host name under which the server is listening */
    public const string PROPERTY_SERVER = "server";
    public const string PROPERTY_IS_CLIENT_ENABLED = "enabled";
    public const string PROPERTY_IS_CLIENT_SENDING_ENABLED = "sending.enabled";
    public const string PROPERTY_IS_CLIENT_RECEIVING_ENABLED = "receiving.enabled";
    public const string ROCKSDB_PATH = "rocksdb_path";
    public const string ROCKSDB_FSYNC_DISABLED = "rocksdb.fsync.disabled";
    public const string PROPERTY_QUEUE = "messaging.queue";
    public const string QUEUE_ID = "id";
    public const string QUEUE_READ_PORT = "portRead";
    public const string QUEUE_WRITE_PORT = "portWrite";
    public const string QUEUE_COPY_INTO_QUEUES = "copyIntoQueues";
    public const string PROPERTY_WHITELIST = PROPERTY_QUEUE + ".copy.into.whitelist";
    public const string WHITELIST_QUEUE_ID = "id";
    public const string WHITELIST_PASSTHROUGH = "passthrough";
    public const string LOG_PATH = "logging.file.path";
    public const string LOG_LEVEL = "logging.file.level";
    public const string LOG_TEMPLATE = "logging.file.template";
}
