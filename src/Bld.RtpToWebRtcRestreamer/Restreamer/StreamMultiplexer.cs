using System.Collections.Immutable;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.Restreamer;

internal class StreamMultiplexer
{
    private readonly ILogger<StreamMultiplexer> _logger;
    private ImmutableDictionary<RTCPeerConnection, MultiplexedPeer> _peers = (new Dictionary<RTCPeerConnection, MultiplexedPeer>()).ToImmutableDictionary();

    public StreamMultiplexer(ILogger<StreamMultiplexer> logger)
    {
        _logger = logger;
    }

    public int ActiveStreamsCount => _peers.Count(pair =>
        pair.Key.connectionState is not (RTCPeerConnectionState.closed or RTCPeerConnectionState.disconnected or RTCPeerConnectionState.closed));

    public void RegisterPeer(RTCPeerConnection peer)
    {
        _peers = _peers.Add(peer, new MultiplexedPeer(peer));

    }

    public void StartPeerTransmit(RTCPeerConnection peer)
    {
        if (_peers.TryGetValue(peer, out var multiplexedPeer))
        {
            multiplexedPeer.Start();
            _logger.LogDebug("Streaming for peer started");
        }
        else
        {
            _logger.LogError("Failed to get peer to start");
        }
    }

    public void StopPeerTransmit(RTCPeerConnection peer)
    {
        if (_peers.TryGetValue(peer, out var multiplexedPeer))
        {
            multiplexedPeer.Stop();
            _logger.LogDebug("Streaming for peer stopped");
        }
        else
        {
            _logger.LogError("Failed to get peer to stop");
        }
    }

    public async Task SendVideoPacketAsync(RtpPacket rtpPacket)
    {
        foreach (var pair in _peers)
        {
            await pair.Value.SendVideoAsync(rtpPacket);
        }
    }

    public void Cleanup()
    {
        var toRemove = _peers.Where(pair =>
                (pair.Key.connectionState is RTCPeerConnectionState.closed or RTCPeerConnectionState.disconnected or RTCPeerConnectionState.closed) ||
                pair.Key.IsClosed)
            .Select(pair => pair.Key)
            .ToList();
        _peers = _peers.RemoveRange(toRemove);
    }
}