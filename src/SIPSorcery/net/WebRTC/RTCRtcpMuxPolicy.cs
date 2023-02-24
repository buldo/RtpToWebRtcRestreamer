namespace SIPSorcery.Net
{
    /// <summary>
    /// The RTCP multiplex options for ICE candidates. This option is currently redundant
    /// since the single option means RTCP multiplexing MUST be available or the SDP negotiation
    /// will fail.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#dom-rtcrtcpmuxpolicy.
    /// </remarks>
    public enum RTCRtcpMuxPolicy
    {
        require
    }
}