using System.Diagnostics.CodeAnalysis;

namespace SIPSorcery.Net
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#dom-rtcicecomponent.
    /// </remarks>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum RTCIceComponent
    {
        rtp = 1,
        rtcp = 2
    }
}