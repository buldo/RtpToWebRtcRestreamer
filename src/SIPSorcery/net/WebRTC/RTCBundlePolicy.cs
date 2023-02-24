namespace SIPSorcery.Net
{
    /// <summary>
    /// Affects which media tracks are negotiated if the remote end point is not bundle aware.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#dom-rtcbundlepolicy.
    /// </remarks>
    public enum RTCBundlePolicy
    {
        balanced,
        max_compat,
        max_bundle
    }
}