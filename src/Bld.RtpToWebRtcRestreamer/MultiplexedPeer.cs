using Bld.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

namespace Bld.RtpToWebRtcRestreamer
{
    internal class MultiplexedPeer
    {
        private readonly RTCPeerConnection _peer;
        private bool _isStarted;

        public MultiplexedPeer(RTCPeerConnection peer)
        {
            _peer = peer;
        }

        public void SendVideo(byte[] sample)
        {
            if (!_isStarted)
            {
                return;
            }

            _peer.SendVideo(1, sample);
        }

        public void SendVideo(RtpPacket packet)
        {
            if (!_isStarted)
            {
                return;
            }

            _peer.SendVideo(packet);
        }

        public void Start()
        {
            _isStarted = true;
        }

        public void Stop()
        {
            _isStarted = false;
        }
    }
}