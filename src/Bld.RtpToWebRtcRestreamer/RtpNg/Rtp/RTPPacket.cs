namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp
{
    internal class RTPPacket
    {
        public readonly RTPHeader Header;
        public readonly byte[] Payload;

        public RTPPacket(int payloadSize)
        {
            Header = new RTPHeader();
            Payload = new byte[payloadSize];
        }

        public RTPPacket(Span<byte> packet)
        {
            Header = new RTPHeader(packet);
            Payload = packet
                .Slice(Header.Length, Header.PayloadSize)
                .ToArray();
        }

        public byte[] GetBytes()
        {
            var header = Header.GetBytes();
            var packet = new byte[header.Length + Payload.Length];

            Array.Copy(header, packet, header.Length);
            Array.Copy(Payload, 0, packet, header.Length, Payload.Length);

            return packet;
        }
    }
}
