/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

public interface IQueueTransmitter
{
    /// <summary>
    /// Starts the transmitter in the background.
    /// </summary>
    void StartAsync();

    /// <summary>
    /// Stops the transmitter.
    /// </summary>
    /// <returns>The number of messages processed or a relevant long metric.</returns>
    long Stop();

    /// <summary>
    /// Returns true if the transmitter is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Returns the unique identifier for this transmitter.
    /// </summary>
    string Id { get; }
}
