namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC
{
    /// <summary>
    /// Determines which ICE candidates can be used for a peer connection.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#dom-rtcicetransportpolicy.
    /// </remarks>
    public enum RTCIceTransportPolicy
    {
        all,
        relay
    }
}