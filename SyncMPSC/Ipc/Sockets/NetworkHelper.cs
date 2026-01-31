/*
 * Copyright (c) 2026           Stefan Zobel.
 *
 * http://www.opensource.org/licenses/mit-license.php
 */
using System.Net;
using System.Net.Sockets;

namespace SyncMPSC.Ipc.Sockets
{
    internal static class NetworkHelper
    {
        /// <summary>
        /// Resolve a host name in an IPEndPoint address and prefer IPv4.
        /// </summary>
        public static IPEndPoint ResolveEndPoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host must not be empty.", nameof(host));

            IPAddress ipAddress;

            // Special case localhost: use direct IPv4 Loopback (127.0.0.1)
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                ipAddress = IPAddress.Loopback;
            }
            // Check whether host is already an IP address, e.g. "192.168.1.1"
            else if (IPAddress.TryParse(host, out var parsedAddress))
            {
                ipAddress = parsedAddress;
            }
            else
            {
                try
                {
                    var addresses = Dns.GetHostAddresses(host);

                    // Prefer IPv4 (InterNetwork) for backwards compatibility
                    ipAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                                ?? addresses.FirstOrDefault()
                                ?? throw new SocketException((int)SocketError.HostNotFound);
                }
                catch (Exception ex)
                {
                    throw new WireException(new IOException($"Host '{host}' couldn't be resolved.", ex));
                }
            }

            return new IPEndPoint(ipAddress, port);
        }
    }
}
