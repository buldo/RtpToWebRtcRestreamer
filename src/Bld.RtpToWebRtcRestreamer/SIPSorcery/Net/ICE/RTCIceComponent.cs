using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE
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