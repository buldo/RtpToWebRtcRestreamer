using System.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    public delegate void PacketReceivedDelegate(UdpReceiver receiver, int localPort, IPEndPoint remoteEndPoint, byte[] packet);
}