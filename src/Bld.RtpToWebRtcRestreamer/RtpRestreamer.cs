using System.Net;
using Bld.RtpToWebRtcRestreamer.Restreamer;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer;

internal class RtpRestreamer
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RtpRestreamer> _logger;
    private readonly PooledUdpSource _receiver;
    private readonly StreamMultiplexer _streamMultiplexer;

    public RtpRestreamer(
        IPEndPoint rtpListenEndpoint,
        ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<RtpRestreamer>();

        _receiver = new(rtpListenEndpoint, _loggerFactory.CreateLogger<PooledUdpSource>());
        _streamMultiplexer = new StreamMultiplexer(_loggerFactory.CreateLogger<StreamMultiplexer>());
    }

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted = true;

        _receiver.Start(RtpProcessorAsync);
    }

    public async Task StopAsync()
    {
        if (!IsStarted)
        {
            return;
        }

        IsStarted = false;
        await _receiver.StopAsync();
        foreach (var peerConnection in _streamMultiplexer.GetAllPeers())
        {
            _streamMultiplexer.ClosePeer(peerConnection.Peer.Id);
        }
    }

    public async Task<(Guid PeerId, string Sdp)> AppendClient()
    {
        var peerConnection = new RTCPeerConnection();
        _streamMultiplexer.RegisterPeer(peerConnection);

        var videoTrack = new MediaStreamTrack(
            new VideoFormat(VideoCodecsEnum.H264, 96),
            MediaStreamStatusEnum.SendOnly);
        peerConnection.AddTrack(videoTrack);

        peerConnection.onconnectionstatechange += (state) =>
        {
            _logger.LogDebug("Peer connection state change to {state}.", state);

            if (state == RTCPeerConnectionState.connected)
            {
                _streamMultiplexer.StartPeerTransmit(peerConnection.Id);
            }
            else if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.closed)
            {
                _streamMultiplexer.ClosePeer(peerConnection.Id);
            }
        };

        // Diagnostics.
        peerConnection.OnReceiveReport += (re, rr) =>
        {
            _logger.LogDebug($"RTCP Receive for from {re}\n{rr.GetDebugSummary()}");
        };

        var answer = peerConnection.CreateOffer();

        return (peerConnection.Id, answer.sdp);
    }

    public async Task ProcessClientAnswerAsync(Guid peerId, string sdpString)
    {
        var peer = _streamMultiplexer.GetById(peerId);
        if (peer != null)
        {
            var result = peer.Peer.setRemoteDescription(new RTCSessionDescriptionInit
            {
                sdp = sdpString,
                type = RTCSdpType.answer
            });
            _logger.LogDebug("setRemoteDescription result: {@result}", result);
        }
    }

    private async Task RtpProcessorAsync(RtpPacket packet)
    {
        await _streamMultiplexer.SendVideoPacketAsync(packet);
        _receiver.ReusePacket(packet);
    }
}