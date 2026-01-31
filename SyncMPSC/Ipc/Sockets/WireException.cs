/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

/// <summary>
/// Exception thrown for wire-level protocol errors.
/// </summary>
public class WireException : Exception
{
    public WireException() : base() { }

    public WireException(string? message) : base(message) { }

    public WireException(string? message, Exception? innerException)
        : base(message, innerException) { }

    public WireException(Exception? cause) : base(null, cause) { }
}
