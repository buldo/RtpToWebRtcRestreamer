using System.Net;

using Bld.RtpToWebRtcRestreamer.Common;

using NetCoreServer;

namespace Bld.RtpToWebRtcRestreamer;

internal class RtpReceiver : UdpServer
{
    private readonly Action<RTPPacket> _rtpProcessor;

    public RtpReceiver(
        IPEndPoint endpoint,
        Action<RTPPacket> rtpProcessor)
        : base(endpoint)
    {
        _rtpProcessor = rtpProcessor;
    }

    protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        if (size > 0)
        {
            var packet = new RTPPacket(buffer.AsSpan(0, (int)size));
            _rtpProcessor(packet);
        }

        ReceiveAsync();
    }

    protected override void OnStarted()
    {
        ReceiveAsync();
    }
}
