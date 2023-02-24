//-----------------------------------------------------------------------------
// Filename: IPSocket.cs
//
// Description: Helper functions for socket strings and IP end points.
// Note that as of 16 Nov 2019 a number of equivalent functions are now
// contained in the System.Net.IPEndPoint v4 class BUT are missing from
// the Net Standard version.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 22 jun 2005	Aaron Clauson   Created, Dublin, Ireland.
// rj2: need some more helper methods
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace SIPSorcery.Sys
{
    public class IPSocket
    {
        /// <summary>
        /// Specifies the maximum acceptable value for the <see cref='System.Net.IPEndPoint'/> Port property.
        /// </summary>
        private const int MaxPort = 0x0000FFFF;

        /// <summary>
        /// This code is based on the IPEndPoint.TryParse method in the dotnet source code at
        /// https://github.com/dotnet/corefx/blob/master/src/System.Net.Primitives/src/System/Net/IPEndPoint.cs.
        /// If/when that feature makes it into .NET Standard this method can be replaced.
        /// </summary>
        /// <param name="s">The end point string to parse.</param>
        /// <param name="result">If the parse is successful this output parameter will contain the IPv4 or IPv6 end point.</param>
        /// <returns>Returns true if the string could be successfully parsed as an IPv4 or IPv6 end point. False if not.</returns>
        public static bool TryParseIPEndPoint(string s, out IPEndPoint result)
        {
            int addressLength = s.Length;  // If there's no port then send the entire string to the address parser
            int lastColonPos = s.LastIndexOf(':');

            // Look to see if this is an IPv6 address with a port.
            if (lastColonPos > 0)
            {
                if (s[lastColonPos - 1] == ']')
                {
                    addressLength = lastColonPos;
                }
                // Look to see if this is IPv4 with a port (IPv6 will have another colon)
                else if (s.Substring(0, lastColonPos).LastIndexOf(':') == -1)
                {
                    addressLength = lastColonPos;
                }
            }

            if (IPAddress.TryParse(s.Substring(0, addressLength), out IPAddress address))
            {
                uint port = 0;
                if (addressLength == s.Length ||
                    (uint.TryParse(s.Substring(addressLength + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= MaxPort))
                {
                    result = new IPEndPoint(address, (int)port);
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Checks the Contact SIP URI host and if it is recognised as a private address it is replaced with the socket
        /// the SIP message was received on.
        /// 
        /// Private address space blocks RFC 1597.
        ///		10.0.0.0        -   10.255.255.255
        ///		172.16.0.0      -   172.31.255.255
        ///		192.168.0.0     -   192.168.255.255
        ///
        /// </summary>
        public static bool IsPrivateAddress(string host)
        {
            if (IPAddress.TryParse(host, out var ipAddress))
            {
                if (IPAddress.IsLoopback(ipAddress) || ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal)
                {
                    return true;
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] addrBytes = ipAddress.GetAddressBytes();
                    if ((addrBytes[0] == 10) ||
                        (addrBytes[0] == 172 && (addrBytes[1] >= 16 && addrBytes[1] <= 31)) ||
                        (addrBytes[0] == 192 && addrBytes[1] == 168))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static IPEndPoint Parse(string endpointstring, int defaultport = -1)
        {
            if (endpointstring.IsNullOrBlank())
            {
                throw new ArgumentException("Endpoint descriptor must not be empty.");
            }

            if (defaultport != -1 &&
                (defaultport < IPEndPoint.MinPort
                || defaultport > IPEndPoint.MaxPort))
            {
                throw new ArgumentException(string.Format("Invalid default port '{0}'", defaultport));
            }

            string[] values = endpointstring.Split(new char[] { ':' });
            IPAddress ipaddr;
            int port = -1;

            //check if we have an IPv6 or ports
            if (values.Length <= 2) // ipv4 or hostname
            {
                if (values.Length == 1)
                {
                    //no port is specified, default
                    port = defaultport;
                }
                else
                {
                    port = getPort(values[1]);
                }

                //try to use the address as IPv4, otherwise get hostname
                if (!IPAddress.TryParse(values[0], out ipaddr))
                {
                    try
                    {
                        ipaddr = getIPfromHost(values[0]);
                    }
                    catch
                    {
                        throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endpointstring));
                    }
                }
            }
            else if (values.Length > 2) //ipv6
            {
                //could [a:b:c]:d
                if (values[0].StartsWith("[") && values[values.Length - 2].EndsWith("]"))
                {
                    string ipaddressstring = string.Join(":", values.Take(values.Length - 1).ToArray());
                    ipaddr = IPAddress.Parse(ipaddressstring);
                    port = getPort(values[values.Length - 1]);
                }
                else //[a:b:c] or a:b:c
                {
                    ipaddr = IPAddress.Parse(endpointstring);
                    port = defaultport;
                }
            }
            else
            {
                throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endpointstring));
            }

            if (port == -1)
            {
                port = 0;
            }

            return new IPEndPoint(ipaddr, port);
        }

        private static int getPort(string p)
        {
            int port;

            if (!int.TryParse(p, out port)
             || port < IPEndPoint.MinPort
             || port > IPEndPoint.MaxPort)
            {
                throw new FormatException(string.Format("Invalid end point port '{0}'", p));
            }

            return port;
        }

        private static IPAddress getIPfromHost(string p)
        {
            try
            {
                var hosts = Dns.GetHostAddresses(p);

                if (hosts == null || hosts.Length == 0)
                {
                    throw new ArgumentException(string.Format("Host not found: {0}", p));
                }
                return hosts[0];
            }
            catch
            {
                throw new ArgumentException(string.Format("Host not found: {0}", p));
            }
        }
    }
}
