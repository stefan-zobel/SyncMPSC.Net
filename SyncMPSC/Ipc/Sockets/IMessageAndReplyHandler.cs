/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

public interface IMessageAndReplyHandler
{
    void OnNext(byte[] message, Stream reply);
}
