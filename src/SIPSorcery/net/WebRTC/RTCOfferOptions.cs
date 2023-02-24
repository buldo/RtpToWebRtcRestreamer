namespace SIPSorcery.Net
{
    public class RTCOfferOptions
    {
        /// <summary>
        /// If set it indicates that any available ICE candidates should NOT be added
        /// to the offer SDP. By default "host" candidates should always be available
        /// and will be added to the offer SDP.
        /// </summary>
        public bool X_ExcludeIceCandidates;
    }
}