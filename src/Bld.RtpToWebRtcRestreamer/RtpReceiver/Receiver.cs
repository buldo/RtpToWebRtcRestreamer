using System.Net;

using Bld.RtpToWebRtcRestreamer.Common;
using Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpReceiver;

internal class Receiver
{
    private readonly IPEndPoint _bindEndPoint;
    private readonly Action<RTPPacket> _rtpProcessor;
    private readonly RTPChannel _channel;

    public Receiver(
        IPEndPoint bindEndPoint,
        ILogger<Receiver> logger,
        Action<RTPPacket> rtpProcessor)
    {
        _bindEndPoint = bindEndPoint;
        _rtpProcessor = rtpProcessor;
        var sessionConfig = new RtpSessionConfig
        {
            BindAddress = _bindEndPoint.Address,
            BindPort = _bindEndPoint.Port,
            IsMediaMultiplexed = false,
        };
        _channel = new RTPChannel(false, sessionConfig.BindAddress, sessionConfig.BindPort, logger);
        _channel.OnRtpDataReceived += OnReceiveRTPPacket;
    }

    public void Start()
    {
        _channel.Start();
    }

    private void OnReceiveRTPPacket(int localPort, IPEndPoint remoteEndPoint, byte[] buffer)
    {
        var rtpPacket = new RTPPacket(buffer);
        _rtpProcessor(rtpPacket);
    }
}
