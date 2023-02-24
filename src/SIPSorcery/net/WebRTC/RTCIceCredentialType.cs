using System.Diagnostics.CodeAnalysis;

namespace SIPSorcery.Net
{
    /// <summary>
    /// The types of credentials for an ICE server.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#rtcicecredentialtype-enum.
    /// </remarks>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum RTCIceCredentialType
    {
        password
    }
}