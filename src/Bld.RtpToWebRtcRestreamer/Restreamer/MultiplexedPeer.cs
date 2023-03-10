using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

namespace Bld.RtpToWebRtcRestreamer.Restreamer;

internal class MultiplexedPeer
{
    private bool _isStarted;

    public MultiplexedPeer(RtcPeerConnection peer)
    {
        Peer = peer;
    }

    public RtcPeerConnection Peer { get;}

    public async Task SendVideoAsync(RtpPacket packet)
    {
        if (!_isStarted)
        {
            return;
        }

        await Peer.SendVideoAsync(packet);
    }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
    }

    public void ClosePeer()
    {
        if (!_isStarted)
        {
            return;
        }
        _isStarted = false;

        Peer.Close("");
    }
}