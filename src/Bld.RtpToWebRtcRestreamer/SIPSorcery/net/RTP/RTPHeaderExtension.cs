namespace SIPSorcery.net.RTP
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
