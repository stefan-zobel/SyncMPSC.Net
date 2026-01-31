/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using System.Net.Sockets;

namespace SyncMPSC.Ipc.Sockets;

internal static class FrameDecoder
{
    public static byte[]? NextFrame(Stream input, byte[] delimiter)
    {
        if (delimiter == null || delimiter.Length == 0)
        {
            throw new ArgumentException("delimiter null or empty", nameof(delimiter));
        }
        ArgumentNullException.ThrowIfNull(input, nameof(input));

        int nextByte;
        try
        {
            nextByte = input.ReadByte();
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // Stream end indicator
            return null;
        }
        catch (ObjectDisposedException)
        {
            // Stream end indicator
            return null;
        }

        if (nextByte == -1)
        {
            // Stream end indicator
            return null;
        }

        if (nextByte == Protocol.ALIVE_REQUEST)
        {
            return Protocol.ALIVE_BYTE;
        }

        int delimiterLength = delimiter.Length;
        int currDelimiterPos = 0;

        if ((byte)nextByte == delimiter[0])
        {
            currDelimiterPos++;
        }

        // Initialize buffer
        using var ms = new MemoryStream();
        ms.WriteByte((byte)nextByte);

        try
        {
            while (currDelimiterPos < delimiterLength && (nextByte = input.ReadByte()) != -1)
            {
                if ((byte)nextByte != delimiter[currDelimiterPos++])
                {
                    currDelimiterPos = 0;
                }
                // Push nextByte into buffer
                ms.WriteByte((byte)nextByte);
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // Stream end indicator
            return null;
        }
        catch (ObjectDisposedException)
        {
            // Stream end indicator
            return null;
        }

        if (currDelimiterPos == delimiterLength)
        {
            // Subtract delimiter length and return adjusted array
            byte[] fullBuffer = ms.GetBuffer();
            int lengthWithoutDelimiter = (int)ms.Length - delimiterLength;

            byte[] result = new byte[lengthWithoutDelimiter];
            Buffer.BlockCopy(fullBuffer, 0, result, 0, lengthWithoutDelimiter);
            return result;
        }

        // Return unmodified buffer
        return ms.ToArray();
    }
}
