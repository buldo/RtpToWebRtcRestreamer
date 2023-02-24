namespace SIPSorcery.Net
{
    /// <summary>
    /// The optional or variable length Type-Length-Value (TLV) parameters
    /// that can be used with INIT and INIT ACK chunks.
    /// </summary>
    public enum SctpInitChunkParameterType : ushort
    {
        IPv4Address = 5,
        IPv6Address = 6,
        StateCookie = 7,                // INIT ACK only.
        UnrecognizedParameter = 8,      // INIT ACK only.
        CookiePreservative = 9,
        HostNameAddress = 11,
        SupportedAddressTypes = 12,
        EcnCapable = 32768
    }
}