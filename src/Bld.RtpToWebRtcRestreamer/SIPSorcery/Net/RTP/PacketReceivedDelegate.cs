using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Networking;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal delegate void PacketReceivedDelegate(UdpSocket socket, int localPort, IPEndPoint remoteEndPoint, byte[] packet);