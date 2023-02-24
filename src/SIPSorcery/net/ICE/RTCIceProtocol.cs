namespace SIPSorcery.Net
{
    /// <summary>
    /// The transport protocol types for an ICE candidate.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#rtciceprotocol-enum.
    /// </remarks>
    public enum RTCIceProtocol
    {
        udp,
        tcp
    }
}