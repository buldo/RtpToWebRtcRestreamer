namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    public class RTPHeaderExtension
    {
        public RTPHeaderExtension(int id, string uri)
        {
            Id = id;
            Uri = uri;
        }
        public int Id { get; }
        public string Uri { get; }
    }
}
