using System.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal delegate void PacketReceivedDelegate(UdpReceiver receiver, int localPort, IPEndPoint remoteEndPoint, byte[] packet);