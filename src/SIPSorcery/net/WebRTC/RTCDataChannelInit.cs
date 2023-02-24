namespace SIPSorcery.Net
{
    public class RTCDataChannelInit
    {
        public bool? ordered { get; set; }
        public ushort? maxPacketLifeTime { get; set; }
        public ushort? maxRetransmits { get; set; }
        public string protocol { get; set; }
        public bool? negotiated { get; set; }
        public ushort? id { get; set; }
    };
}