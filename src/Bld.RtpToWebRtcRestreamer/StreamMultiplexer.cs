using System.Collections.Concurrent;
using Bld.RtpToWebRtcRestreamer.Common;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer
{
    internal class StreamMultiplexer
    {
        private readonly ILogger<StreamMultiplexer> _logger;
        private readonly ConcurrentDictionary<RTCPeerConnection, MultiplexedPeer> _peers = new();

        public StreamMultiplexer(
            ILogger<StreamMultiplexer> logger)
        {
            _logger = logger;
        }

        public int ActiveStreamsCount => _peers.Count(pair =>
            pair.Key.connectionState is not (RTCPeerConnectionState.closed or RTCPeerConnectionState.disconnected or RTCPeerConnectionState.closed));

        public void RegisterPeer(RTCPeerConnection peer)
        {
            var result = _peers.TryAdd(peer, new MultiplexedPeer(peer));
            if (result)
            {
                _logger.LogDebug("Peer added");
            }
            else
            {
                _logger.LogError("Peer adding error");
            }
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

        public void SendVideoPacket(RTPPacket rtpPacket)
        {
            foreach (var streamMultiplexer in _peers.Values) {
                streamMultiplexer.SendVideo(rtpPacket);
            }
        }

        public void Cleanup()
        {
            var toRemove = _peers.Where(pair =>
                    pair.Key.connectionState is (RTCPeerConnectionState.closed or RTCPeerConnectionState.disconnected
                        or RTCPeerConnectionState.closed))
                .ToList();
            foreach (var multiplexedPeer in toRemove)
            {
                _peers.TryRemove(multiplexedPeer);
            }
        }
    }
}