namespace SIPSorcery.Net
{
    /// <summary>
    /// Options for creating an SDP answer.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#dictionary-rtcofferansweroptions-members.
    /// </remarks>
    public class RTCAnswerOptions
    {
        /// If set it indicates that any available ICE candidates should NOT be added
        /// to the offer SDP. By default "host" candidates should always be available
        /// and will be added to the offer SDP.
        /// </summary>
        public bool X_ExcludeIceCandidates;
    }
}