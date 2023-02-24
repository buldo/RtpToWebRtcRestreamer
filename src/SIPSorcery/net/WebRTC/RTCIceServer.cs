namespace SIPSorcery.Net
{
    /// <summary>
    /// Used to specify properties for a STUN or TURN server that can be used by an ICE agent.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#rtciceserver-dictionary.
    /// </remarks>
    public class RTCIceServer
    {
        public string urls;
        public string username;
        public RTCIceCredentialType credentialType;
        public string credential;
    }
}