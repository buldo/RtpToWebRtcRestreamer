using System.Net;

using Bld.RtpToWebRtcRestreamer.Common;
using Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpReceiver;

internal class Receiver
{
    private readonly Action<RTPPacket> _rtpProcessor;
    private readonly RTPChannel _channel;

    public Receiver(
        IPEndPoint bindEndPoint,
        ILogger<Receiver> logger,
        Action<RTPPacket> rtpProcessor)
    {
        _rtpProcessor = rtpProcessor;
        _channel = new RTPChannel(bindEndPoint.Address, bindEndPoint.Port, logger);
        _channel.OnRtpDataReceived += OnReceiveRTPPacket;
    }

    public void Start()
    {
        _channel.Start();
    }

    private void OnReceiveRTPPacket(RTPPacket packet)
    {
        _rtpProcessor(packet);
    }
}
