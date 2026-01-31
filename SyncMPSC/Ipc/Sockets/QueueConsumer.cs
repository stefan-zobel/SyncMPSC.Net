/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using librocks.Net;
using System.Net.Sockets;

namespace SyncMPSC.Ipc.Sockets;

public sealed class QueueConsumer
{
    private static readonly byte[] DELIMITER = (byte[])Protocol.END_OF_REQUEST.Clone();

    private readonly TcpClient _client;
    private readonly Stream _stream;
    private readonly byte[] _reply = new byte[Protocol.SIZE];
    private readonly IQueueTransmitter _tm;

    public QueueConsumer(TcpClient client, IQueueTransmitter tm)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _stream = client.GetStream();
        _tm = tm ?? throw new ArgumentNullException(nameof(tm));
    }

    public bool Accept(NativeBytes message)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        bool success = false;

        if (message is { Length: > 0 } && _tm.IsRunning && IsSocketReady())
        {
            try
            {
                using var ms = new MemoryStream();
                ms.Write(message.Span);
                ms.Write(DELIMITER);
                byte[] frameData = ms.ToArray();

                if (_tm.IsRunning && IsSocketReady())
                {
                    try
                    {
                        // Prepend with a LF (0x0A) in a separate write to reliably
                        // detect a broken connection at the kernel level.
                        _stream.WriteByte(0x0A);

                        // Write the actual message frame
                        _stream.Write(frameData, 0, frameData.Length);
                        _stream.Flush();

                        // Read the reply
                        int bytesRead = _stream.Read(_reply, 0, Protocol.SIZE);
                        if (bytesRead != Protocol.SIZE)
                        {
                            return false;
                        }

                        if (Protocol.IsOkReply(_reply))
                        {
                            _reply[0] = 0;
                            success = true;
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException)
                    {
                        CloseAll();
                    }
                }
            }
            catch (Exception)
            {
                CloseAll();
            }
        }

        // TODO: Roundtrip measurement logic here
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);

        return success;
    }

    private bool IsSocketReady()
    {
        // .NET TcpClient.Connected property combined with a check for the underlying socket
        return _client.Connected && !_client.Client.Poll(0, SelectMode.SelectError);
    }

    private void CloseAll()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
