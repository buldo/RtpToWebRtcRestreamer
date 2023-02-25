using System.Net;

using Bld.RtpToWebRtcRestreamer.Common;
using Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpReceiver;

internal class Receiver
{
    private static int _nextIndex;

    private readonly IPEndPoint _bindEndPoint;
    private readonly Action<RTPPacket> _rtpProcessor;
    private readonly VideoStream _videoStream;
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
        _videoStream = new VideoStream(_nextIndex, logger);
        _videoStream.OnVideoFrameReceivedByIndex += VideoStreamOnOnVideoFrameReceivedByIndex;
        _channel = new RTPChannel(false, sessionConfig.BindAddress, sessionConfig.BindPort, logger);
        _channel.OnRtpDataReceived += OnReceiveRTPPacket;

        _nextIndex++;
    }

    public event Action<int, IPEndPoint, uint, byte[]> OnVideoFrameReceivedByIndex;

    public void Start()
    {
        _channel.Start();
    }

    private void VideoStreamOnOnVideoFrameReceivedByIndex(int arg1, IPEndPoint arg2, uint arg3, byte[] arg4)
    {
        OnVideoFrameReceivedByIndex?.Invoke(arg1, arg2, arg3, arg4);
    }

    private void OnReceiveRTPPacket(int localPort, IPEndPoint remoteEndPoint, byte[] buffer)
    {
        var rtpPacket = new RTPPacket(buffer);
        _videoStream.OnReceiveRTPPacket(rtpPacket, remoteEndPoint);
        _rtpProcessor(rtpPacket);
    }
}
