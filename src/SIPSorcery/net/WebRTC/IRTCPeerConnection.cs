//-----------------------------------------------------------------------------
// Filename: IRTCPeerConnection.cs
//
// Description: Contains the interface definition for the RTCPeerConnection
// class as defined by the W3C WebRTC specification. Should be kept up to 
// date with:
// https://www.w3.org/TR/webrtc/#interface-definition
//
// See also:
// https://tools.ietf.org/html/draft-ietf-rtcweb-jsep-25#section-3.5.4
//
// Author(s):
// Aaron Clauson
//
// History:
// 16 Mar 2020	Aaron Clauson	Created.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace SIPSorcery.Net
{
    public interface IRTCPeerConnection
    {
        //IRTCPeerConnection(RTCConfiguration configuration = null);
        RTCSessionDescriptionInit createOffer(RTCOfferOptions options = null);
        RTCSessionDescriptionInit createAnswer(RTCAnswerOptions options = null);
        Task setLocalDescription(RTCSessionDescriptionInit description);
        RTCSessionDescription localDescription { get; }
        RTCSessionDescription currentLocalDescription { get; }
        RTCSessionDescription pendingLocalDescription { get; }
        SetDescriptionResultEnum setRemoteDescription(RTCSessionDescriptionInit description);
        RTCSessionDescription remoteDescription { get; }
        RTCSessionDescription currentRemoteDescription { get; }
        RTCSessionDescription pendingRemoteDescription { get; }
        void addIceCandidate(RTCIceCandidateInit candidate = null);
        RTCSignalingState signalingState { get; }
        RTCIceGatheringState iceGatheringState { get; }
        RTCIceConnectionState iceConnectionState { get; }
        RTCPeerConnectionState connectionState { get; }
        bool canTrickleIceCandidates { get; }
        void restartIce();
        RTCConfiguration getConfiguration();
        void setConfiguration(RTCConfiguration configuration = null);
        void close();
        event Action onnegotiationneeded;
        event Action<RTCIceCandidate> onicecandidate;
        event Action<RTCIceCandidate, string> onicecandidateerror;
        event Action onsignalingstatechange;
        event Action<RTCIceConnectionState> oniceconnectionstatechange;
        event Action<RTCIceGatheringState> onicegatheringstatechange;
        event Action<RTCPeerConnectionState> onconnectionstatechange;

        // TODO: Extensions for the RTCMediaAPI
        // https://www.w3.org/TR/webrtc/#rtcpeerconnection-interface-extensions.
        //List<IRTCRtpSender> getSenders();
        //List<IRTCRtpReceiver> getReceivers();
        //List<RTCRtpTransceiver> getTransceivers();
        //RTCRtpSender addTrack(MediaStreamTrack track, param MediaStream[] streams);
        //void removeTrack(RTCRtpSender sender);
        //RTCRtpTransceiver addTransceiver((MediaStreamTrack or DOMString) trackOrKind,
        ////optional RTCRtpTransceiverInit init = {});
        //event ontrack;
    };
}
