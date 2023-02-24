namespace SIPSorcery.SIP
{
    /// <summary>
    /// A list of the transport layer protocols that are supported (the network layers
    /// supported are IPv4 and IPv6).
    /// </summary>
    public enum SIPProtocolsEnum
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
        /// Web Socket.
        /// </summary>
        ws = 4,
        /// <summary>
        /// Web Socket over TLS.
        /// </summary>
        wss = 5,
    }
}