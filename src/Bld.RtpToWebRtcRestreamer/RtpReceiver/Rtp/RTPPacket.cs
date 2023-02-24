namespace Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;

internal class RTPPacket
{
    public RTPHeader Header;
    public byte[] Payload;

    public RTPPacket(byte[] packet)
    {
        Header = new RTPHeader(packet);
        Payload = new byte[Header.PayloadSize];
        Array.Copy(packet, Header.Length, Payload, 0, Payload.Length);
    }
}