/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using System.Net;

namespace SyncMPSC.Ipc.Sockets;

public interface IQueueReceiver
{
    /// <summary>
    /// Connect to the Queue synchronously and then start the receiver thread
    /// asynchronously.
    /// </summary>
    void Start();

    /// <summary>
    /// Gets the unique identifier for this receiver.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the network endpoint (IP and Port) of the server.
    /// </summary>
    IPEndPoint ServerAddress { get; }

    /// <summary>
    /// Returns true if the receiver is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Stops the receiver and returns the duration or status code.
    /// </summary>
    /// <returns>The stop duration in milliseconds or relevant metric.</returns>
    long Stop();
}
