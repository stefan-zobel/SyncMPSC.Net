/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
namespace SyncMPSC.Ipc.Sockets;

public static class Protocol
{
    /// <summary>
    /// The length of most protocol messages
    /// </summary>
    public const int SIZE = 4;

    /// <summary>
    /// The ALIVE_BYTE constant
    /// </summary>
    public static readonly byte[] ALIVE_BYTE = [0xFF];

    /// <summary>
    /// The ALIVE_BYTE as an integer
    /// </summary>
    public const int ALIVE_REQUEST = 0xFF;

    /// <summary>
    /// The END_OF_REQUEST constant
    /// </summary>
    public static readonly byte[] END_OF_REQUEST = [0xF5, 0xF7, 0xF9, 0xC1];

    /// <summary>
    /// The REPLY_OK (ACK) constant
    /// </summary>
    public static readonly byte[] REPLY_OK = [0xF6, 0xF8, 0xFA, 0xC0];

    /// <summary>
    /// The REPLY_FAILURE constant
    /// </summary>
    public static readonly byte[] REPLY_FAILURE = [0xC0, 0xFB, 0xFC, 0xFD];

    /// <summary>
    /// Returns true when the given reply byte array is the OK reply
    /// </summary>
    public static bool IsOkReply(byte[]? reply)
    {
        if (reply != null && reply.Length == SIZE)
        {
            // Using explicit indices as in original for performance, 
            // though Span.SequenceEqual would be equally fast here.
            return reply[0] == 0xF6 && reply[1] == 0xF8 && reply[2] == 0xFA && reply[3] == 0xC0;
        }
        return false;
    }

    /// <summary>
    /// Returns true when the given request byte array is the ALIVE request
    /// </summary>
    public static bool IsAliveRequest(byte[]? request)
    {
        if (request != null && request.Length == 1)
        {
            return request[0] == ALIVE_REQUEST;
        }
        return false;
    }
}
