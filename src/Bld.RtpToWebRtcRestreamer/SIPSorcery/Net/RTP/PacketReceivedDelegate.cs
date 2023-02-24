using System.Net;

namespace SIPSorcery.Net
{
    public delegate void PacketReceivedDelegate(UdpReceiver receiver, int localPort, IPEndPoint remoteEndPoint, byte[] packet);
}