/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using librocks.Net;
using Microsoft.Extensions.Logging;

namespace SyncMPSC.Ipc.Sockets;

public sealed class QueueProducer : IMessageAndReplyHandler
{
    private static readonly ILogger<QueueProducer> LOGGER = LogManager.GetLogger<QueueProducer>();

    private readonly string _id;
    private readonly Queue _mainKueue;
    private readonly Queue?[] _copyKueues;
    private readonly IDictionary<string, IMessagePassthrough>? _copyPassThroughs;
    private readonly QueueManager? _km;

    public QueueProducer(
        string id,
        Queue kueue,
        HashSet<string>? additionalWriteQueues,
        IDictionary<string, IMessagePassthrough>? copyPassThroughs,
        QueueManager? km)
    {
        _mainKueue = kueue ?? throw new ArgumentNullException(nameof(kueue));
        _km = km;
        _id = id;
        _copyKueues = GetAdditionalQueues(kueue, additionalWriteQueues);
        _copyPassThroughs = copyPassThroughs;
    }

    private Queue?[] GetAdditionalQueues(Queue kueue, HashSet<string>? additionalWriteQueues)
    {
        if (additionalWriteQueues != null && additionalWriteQueues.Count > 0)
        {
            var copyQueues = new Queue?[additionalWriteQueues.Count];
            int i = 0;
            foreach (string furtherQueue in additionalWriteQueues)
            {
                Queue? additionalWriteQueue = _km?.Get(furtherQueue);
                copyQueues[i++] = additionalWriteQueue;
            }
            return copyQueues;
         }
        return [];
    }

    public void OnNext(byte[]? message, Stream reply)
    {
        if (message == null || message.Length == 0)
        {
            reply.Write(Protocol.REPLY_FAILURE, 0, Protocol.REPLY_FAILURE.Length);
        }
        else
        {
            Queue?[] more = _copyKueues;
            try
            {
                _mainKueue.Put(message);

                foreach (var queue in more)
                {
                    bool shouldPut = true;
                    if (_copyPassThroughs != null && _copyPassThroughs.Count > 0)
                    {
                        if (queue != null && _copyPassThroughs.TryGetValue(queue.Identifier, out var passThrough))
                        {
                            if (passThrough != null && !passThrough.Accepts(message))
                            {
                                shouldPut = false;
                            }
                        }
                    }

                    if (shouldPut)
                    {
                        queue?.Put(message);
                    }
                }

                reply.Write(Protocol.REPLY_OK, 0, Protocol.REPLY_OK.Length);
            }
            catch (Exception ex)
            {
                LOGGER.LogWarning(ex, "Messaging error in producer {Id}", _id);
                reply.Write(Protocol.REPLY_FAILURE, 0, Protocol.REPLY_FAILURE.Length);
            }
        }
        reply.Flush();
    }
}
