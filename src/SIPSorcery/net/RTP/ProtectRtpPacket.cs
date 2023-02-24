namespace SIPSorcery.Net
{
    public delegate int ProtectRtpPacket(byte[] payload, int length, out int outputBufferLength);
}