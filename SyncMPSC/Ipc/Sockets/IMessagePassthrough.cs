/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

public interface IMessagePassthrough
{
    /// <summary>
    /// Returns true if the message is accepted for passthrough.
    /// </summary>
    /// <param name="message">The byte array payload to process.</param>
    bool Accepts(byte[] message);
}
