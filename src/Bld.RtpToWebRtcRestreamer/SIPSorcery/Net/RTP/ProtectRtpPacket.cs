namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    internal delegate int ProtectRtpPacket(byte[] payload, int length, out int outputBufferLength);
}