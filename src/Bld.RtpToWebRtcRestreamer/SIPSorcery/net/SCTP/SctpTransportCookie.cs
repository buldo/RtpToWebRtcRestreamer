namespace SIPSorcery.Net
{
    /// <summary>
    /// The opaque cookie structure that will be sent in response to an SCTP INIT
    /// packet.
    /// </summary>
    public struct SctpTransportCookie
    {
        public static SctpTransportCookie Empty = new SctpTransportCookie { _isEmpty = true };

        public ushort SourcePort { get; set; }
        public ushort DestinationPort { get; set; }
        public uint RemoteTag { get; set; }
        public uint RemoteTSN { get; set; }
        public uint RemoteARwnd { get; set; }
        public string RemoteEndPoint { get; set; }
        public uint Tag { get; set; }
        public uint TSN { get; set; }
        public uint ARwnd { get; set; }
        public string CreatedAt { get; set; }
        public int Lifetime { get; set; }
        public string HMAC { get; set; }

        private bool _isEmpty;

        public bool IsEmpty()
        {
            return _isEmpty;
        }
    }
}