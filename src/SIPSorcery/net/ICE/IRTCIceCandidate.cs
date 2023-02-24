//-----------------------------------------------------------------------------
// Filename: IRTCIceCandidate.cs
//
// Description: Contains the interface definition for the RTCIceCandidate
// class as defined by the W3C WebRTC specification. Should be kept up to 
// date with:
// https://www.w3.org/TR/webrtc/#rtcicecandidate-interface
//
// History:
// 16 Mar 2020	Aaron Clauson	Created.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace SIPSorcery.Net
{
    /// <remarks>
    /// As defined in: https://www.w3.org/TR/webrtc/#rtcicecandidate-interface
    /// 
    /// Rhe 'priority` field was adjusted from ulong to uint due to an issue that 
    /// occurred with the STUN PRIORITY attribute being rejected for not being 4 bytes.
    /// The ICE and WebRTC specifications are contradictory so went with the same as
    /// libwebrtc which is 4 bytes.
    /// See https://github.com/sipsorcery/sipsorcery/issues/350.
    /// </remarks>
    public interface IRTCIceCandidate
    {
        //constructor(optional RTCIceCandidateInit candidateInitDict = { });
        string candidate { get; }
        string sdpMid { get; }
        ushort sdpMLineIndex { get; }
        string foundation { get; }
        RTCIceComponent component { get; }
        uint priority { get; }
        string address { get; }
        RTCIceProtocol protocol { get; }
        ushort port { get; }
        RTCIceCandidateType type { get; }
        RTCIceTcpCandidateType tcpType { get; }
        string relatedAddress { get; }
        ushort relatedPort { get; }
        string usernameFragment { get; }
        //RTCIceCandidateInit toJSON();
        string toJSON();
    }
}
