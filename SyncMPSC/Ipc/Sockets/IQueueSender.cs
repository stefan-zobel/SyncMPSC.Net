/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

public interface IQueueSender : IDisposable
{
    /// <summary>
    /// Returns the unique identifier for this sender.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Sends a message via the underlying transport.
    /// </summary>
    /// <param name="message">The byte array payload to send.</param>
    /// <returns>True if the message was successfully sent and acknowledged.</returns>
    bool SendMessage(byte[] message);

    /// <summary>
    /// Performs a graceful shutdown of the sender resources.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Returns true if this sender uses Inter-Process Communication.
    /// </summary>
    bool IsInterProcessComm { get; }
}
