namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal delegate int ProtectRtpPacket(byte[] payload, int length, out int outputBufferLength);

internal class SecureContext
{
    public DtlsSrtpTransport RtpTransport { get; }

    public ProtectRtpPacket ProtectRtcpPacket { get; }

    public ProtectRtpPacket UnprotectRtcpPacket { get; }

    public SecureContext(DtlsSrtpTransport rtpTransport, ProtectRtpPacket protectRtcpPacket, ProtectRtpPacket unprotectRtcpPacket)
    {
        RtpTransport = rtpTransport;
        ProtectRtcpPacket = protectRtcpPacket;
        UnprotectRtcpPacket = unprotectRtcpPacket;
    }
}