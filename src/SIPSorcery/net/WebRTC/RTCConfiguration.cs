using System.Collections.Generic;
using System.Net;

namespace SIPSorcery.Net
{
    /// <summary>
    /// Defines the parameters to configure how a new RTCPeerConnection is created.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#rtcconfiguration-dictionary.
    /// </remarks>
    public class RTCConfiguration
    {
        public List<RTCIceServer> iceServers;
        public RTCIceTransportPolicy iceTransportPolicy;
        public RTCBundlePolicy bundlePolicy;
        public RTCRtcpMuxPolicy rtcpMuxPolicy;
#pragma warning disable CS0618 // Type or member is obsolete
        public List<RTCCertificate> certificates;
#pragma warning restore CS0618 // Type or member is obsolete
        public List<RTCCertificate2> certificates2;

        /// <summary>
        /// The Bouncy Castle DTLS logic enforces the use of Extended Master 
        /// Secret Keys as per RFC7627. Some WebRTC implementations do not support
        /// Extended Master Secret Keys (for example Kurento in Mar 2021) and this 
        /// configuration option is made available for cases where an application
        /// explicitly decides it's acceptable to disable them.
        /// </summary>
        /// <remarks>
        /// From  https://tools.ietf.org/html/rfc7627#section-4:
        /// "Clients and servers SHOULD NOT accept handshakes that do not use the
        /// extended master secret, especially if they rely on features like
        /// compound authentication that fall into the vulnerable cases described
        /// in Section 6.1."
        /// </remarks>
        public bool X_DisableExtendedMasterSecretKey;

        /// <summary>
        /// Size of the pre-fetched ICE pool. Defaults to 0.
        /// </summary>
        public int iceCandidatePoolSize = 0;

        /// <summary>
        /// Optional. If specified this address will be used as the bind address for any RTP
        /// and control sockets created. Generally this address does not need to be set. The default behaviour
        /// is to bind to [::] or 0.0.0.0, depending on system support, which minimises network routing
        /// causing connection issues.
        /// </summary>
        public IPAddress X_BindAddress;

        /// <summary>
        /// Optional. If set to true the feedback profile set in the SDP offers and answers will be
        /// UDP/TLS/RTP/SAVPF instead of UDP/TLS/RTP/SAVP.
        /// </summary>
        public bool X_UseRtpFeedbackProfile;

        /// <summary>
        /// When gathering host ICE candidates for the local machine the default behaviour is
        /// to only use IP addresses on the interface that the OS routing table selects to connect
        /// to the destination, or the Internet facing interface if the destination is unknown.
        /// This default behaviour is to shield the leaking of all local IP addresses into ICE 
        /// candidates. In some circumstances, and after weighing up the security concerns, 
        /// it's very useful to include all interfaces in when generating the address list. 
        /// Setting this parameter to true will cause all interfaces to be used irrespective of 
        /// the destination address
        /// </summary>
        public bool X_ICEIncludeAllInterfaceAddresses;
    }
}