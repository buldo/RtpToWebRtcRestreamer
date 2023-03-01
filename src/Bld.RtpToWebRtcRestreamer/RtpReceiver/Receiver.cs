using System.Net;
using Bld.Rtp;
using Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpReceiver;

internal class Receiver
{
    private readonly Action<RtpPacket> _rtpProcessor;
    private readonly RTPChannel _channel;

    public Receiver(
        IPEndPoint bindEndPoint,
        ILogger<Receiver> logger,
        Action<RtpPacket> rtpProcessor)
    {
        _rtpProcessor = rtpProcessor;
        _channel = new RTPChannel(bindEndPoint.Address, bindEndPoint.Port, logger);
        _channel.OnRtpDataReceived += OnReceiveRTPPacket;
    }

    public void Start()
    {
        _channel.Start();
    }

    private void OnReceiveRTPPacket(RtpPacket packet)
    {
        _rtpProcessor(packet);
    }
}
