/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

internal class AnyMessagePassthrough : IMessagePassthrough
{
    public bool Accepts(byte[] message)
    {
        return true;
    }
}
