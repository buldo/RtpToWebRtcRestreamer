using System.Diagnostics.CodeAnalysis;

namespace SIPSorcery.Net
{
    /// <summary>
    /// A list of the transport layer protocols that are supported by STUNand TURN (the network layers
    /// supported are IPv4 mad IPv6).
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum STUNProtocolsEnum
    {
        /// <summary>
        /// User Datagram Protocol.
        /// </summary>
        udp = 1,
        /// <summary>.
        /// Transmission Control Protocol
        /// </summary>
        tcp = 2,
        /// <summary>
        /// Transport Layer Security.
        /// </summary>
        tls = 3,
        /// <summary>
        /// Transport Layer Security over UDP.
        /// </summary>
        dtls = 4,
    }
}