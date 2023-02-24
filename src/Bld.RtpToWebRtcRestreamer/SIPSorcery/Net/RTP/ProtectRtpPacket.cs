namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    public delegate int ProtectRtpPacket(byte[] payload, int length, out int outputBufferLength);
}