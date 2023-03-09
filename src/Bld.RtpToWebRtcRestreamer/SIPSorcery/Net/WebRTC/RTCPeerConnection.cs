//-----------------------------------------------------------------------------
// Filename: RTCPeerConnection.cs
//
// Description: Represents a WebRTC RTCPeerConnection.
//
// Specification Soup (as of 13 Jul 2020):
// - "Session Description Protocol (SDP) Offer/Answer procedures for
//   Interactive Connectivity Establishment(ICE)" [ed: specification for
//   including ICE candidates in SDP]:
//   https://tools.ietf.org/html/rfc8839
// - "Session Description Protocol (SDP) Offer/Answer Procedures For Stream
//   Control Transmission Protocol(SCTP) over Datagram Transport Layer
//   Security(DTLS) Transport." [ed: specification for negotiating
//   data channels in SDP, this defines the SDP "sctp-port" attribute]
//   https://tools.ietf.org/html/rfc8841
// - "SDP-based Data Channel Negotiation" [ed: not currently implemented,
//   actually seems like a big pain to implement this given it can already
//   be done in-band on the SCTP connection]:
//   https://tools.ietf.org/html/rfc8864
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 04 Mar 2016	Aaron Clauson	Created.
// 25 Aug 2019  Aaron Clauson   Updated from video only to audio and video.
// 18 Jan 2020  Aaron Clauson   Combined WebRTCPeer and WebRTCSession.
// 16 Mar 2020  Aaron Clauson   Refactored to support RTCPeerConnection interface.
// 13 Jul 2020  Aaron Clauson   Added data channel support.
// 22 Mar 2021  Aaron Clauson   Refactored data channels logic for new SCTP
//                              implementation.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Buffers.Binary;
using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
///     Represents a WebRTC RTCPeerConnection.
/// </summary>
/// <remarks>
///     Interface is defined in https://www.w3.org/TR/webrtc/#interface-definition.
///     The Session Description offer/answer mechanisms are detailed in
///     https://tools.ietf.org/html/rfc8829 "JavaScript Session Establishment Protocol (JSEP)".
/// </remarks>
internal class RTCPeerConnection : IDisposable
{
    // SDP constants.
    //private new const string RTP_MEDIA_PROFILE = "RTP/SAVP";
    private const string RTP_MEDIA_NON_FEEDBACK_PROFILE = "UDP/TLS/RTP/SAVP";
    private const string RTP_MEDIA_DATACHANNEL_DTLS_PROFILE = "DTLS/SCTP"; // Legacy.
    private const string RTP_MEDIA_DATACHANNEL_UDPDTLS_PROFILE = "UDP/DTLS/SCTP";
    private const string SDP_DATACHANNEL_FORMAT_ID = "webrtc-datachannel";

    private const string
        RTCP_MUX_ATTRIBUTE = "a=rtcp-mux"; // Indicates the media announcement is using multiplexed RTCP.

    private const string BUNDLE_ATTRIBUTE = "BUNDLE";
    private const string ICE_OPTIONS = "ice2,trickle"; // Supported ICE options.
    private const ushort SCTP_DEFAULT_PORT = 5000;

    /// <summary>
    ///     The period to wait for the SCTP association to complete before giving up.
    ///     In theory this should be very quick as the DTLS connection should already have been established
    ///     and the SCTP logic only needs to send the small handshake messages to establish
    ///     the association.
    /// </summary>
    private const int SCTP_ASSOCIATE_TIMEOUT_SECONDS = 2;


    /// <summary>
    ///     From libsrtp: SRTP_MAX_TRAILER_LEN is the maximum length of the SRTP trailer
    ///     (authentication tag and MKI) supported by libSRTP.This value is
    ///     the maximum number of octets that will be added to an RTP packet by
    ///     srtp_protect().
    ///     srtp_protect():
    ///     @warning This function assumes that it can write SRTP_MAX_TRAILER_LEN
    ///     into the location in memory immediately following the RTP packet.
    ///     Callers MUST ensure that this much writeable memory is available in
    ///     the buffer that holds the RTP packet.
    ///     srtp_protect_rtcp():
    ///     @warning This function assumes that it can write SRTP_MAX_TRAILER_LEN+4
    ///     to the location in memory immediately following the RTCP packet.
    ///     Callers MUST ensure that this much writeable memory is available in
    ///     the buffer that holds the RTCP packet.
    /// </summary>
    public const int SRTP_MAX_PREFIX_LENGTH = 148;

    /// <summary>
    ///     When there are no RTP packets being sent for an audio or video stream webrtc.lib
    ///     still sends RTCP Receiver Reports with this hard coded SSRC. No doubt it's defined
    ///     in an RFC somewhere but I wasn't able to find it from a quick search.
    /// </summary>
    private const uint RTCP_RR_NOSTREAM_SSRC = 4195875351U;

    protected static readonly ILogger Logger = Log.Logger;

    /// <summary>
    ///     Local ICE candidates that have been supplied directly by the application.
    ///     Useful for cases where the application may has extra information about the
    ///     network set up such as 1:1 NATs as used by Azure and AWS.
    /// </summary>
    private readonly List<RTCIceCandidate> _applicationIceCandidates = new();

    private readonly List<List<SDPSsrcAttribute>> _audioRemoteSdpSsrcAttributes = new();

    private readonly Certificate _dtlsCertificate;
    private readonly AsymmetricKeyParameter _dtlsPrivateKey;
    private readonly Task _iceGatheringTask;
    private readonly object _renegotiationLock = new();
    private readonly List<List<SDPSsrcAttribute>> _videoRemoteSdpSsrcAttributes = new();

    private readonly RTCDataChannelCollection dataChannels;
    private readonly string RTCP_ATTRIBUTE = $"a=rtcp:{SDP.SDP.IGNORE_RTP_PORT_NUMBER} IN IP4 0.0.0.0";

    private readonly string RTP_MEDIA_PROFILE = RTP_MEDIA_NON_FEEDBACK_PROFILE;

    protected readonly RtpSessionConfig RtpSessionConfig;

    private CancellationTokenSource _cancellationSource = new();
    private DtlsSrtpTransport _dtlsHandle;

    // The stream used for the underlying RTP session to create a single RTP channel that will
    // be used to multiplex all required media streams. (see addSingleTrack())

    internal int MRtpChannelsCount; // Need to know the number of RTP Channels

    protected RTPChannel MultiplexRtpChannel;

    /// <summary>
    ///     Constructor to create a new RTC peer connection instance.
    /// </summary>
    public RTCPeerConnection()
    {
        RtpSessionConfig = new RtpSessionConfig
        {
            IsMediaMultiplexed = true,
            IsRtcpMultiplexed = true,
            RtpSecureMediaOption = RtpSecureMediaOptionEnum.DtlsSrtp,
            BindAddress = null,
            BindPort = 0
        };

        dataChannels = new RTCDataChannelCollection(() => _dtlsHandle.IsClient);

        // No certificate was provided so create a new self signed one.
        (_dtlsCertificate, _dtlsPrivateKey) =
            DtlsUtils.CreateSelfSignedTlsCert(ProtocolVersion.DTLSv12, new BcTlsCrypto());

        DtlsCertificateFingerprint = DtlsUtils.Fingerprint(_dtlsCertificate);

        LocalSdpSessionID = Crypto.GetRandomInt(5).ToString();

        // Request the underlying RTP session to create a single RTP channel that will
        // be used to multiplex all required media streams.
        AddSingleTrack(false);

        RtpIceChannel = GetRtpChannel();

        RtpIceChannel.OnIceCandidate += candidate => _onIceCandidate?.Invoke(candidate);
        RtpIceChannel.OnIceConnectionStateChange += IceConnectionStateChange;
        RtpIceChannel.OnIceGatheringStateChange += state => onicegatheringstatechange?.Invoke(state);
        RtpIceChannel.OnIceCandidateError += (candidate, error) => onicecandidateerror?.Invoke(candidate, error);

        OnRtpClosed += Close;
        OnRtcpBye += Close;

        //Cancel Negotiation Task Event to Prevent Duplicated Calls
        onnegotiationneeded += CancelOnNegotiationNeededTask;

        sctp = new RTCSctpTransport(SCTP_DEFAULT_PORT, SCTP_DEFAULT_PORT, RtpIceChannel.RTPPort);

        onnegotiationneeded?.Invoke();

        // This is the point the ICE session potentially starts contacting STUN and TURN servers.
        // This job was moved to a background thread as it was observed that interacting with the OS network
        // calls and/or initialising DNS was taking up to 600ms, see
        // https://github.com/sipsorcery-org/sipsorcery/issues/456.
        _iceGatheringTask = Task.Run(RtpIceChannel.StartGathering);
    }

    private string LocalSdpSessionID { get; }

    public RtpIceChannel RtpIceChannel { get; }
    private IReadOnlyCollection<RTCDataChannel> DataChannels => dataChannels;

    /// <summary>
    ///     The ICE role the peer is acting in.
    /// </summary>
    private IceRolesEnum IceRole { get; set; } = IceRolesEnum.actpass;

    /// <summary>
    ///     The DTLS fingerprint supplied by the remote peer in their SDP. Needs to be checked
    ///     that the certificate supplied during the DTLS handshake matches.
    /// </summary>
    private RTCDtlsFingerprint RemotePeerDtlsFingerprint { get; set; }

    private RTCSessionDescription remoteDescription { get; set; }

    public RTCSignalingState signalingState { get; private set; } = RTCSignalingState.closed;

    private RTCIceConnectionState iceConnectionState =>
        RtpIceChannel != null ? RtpIceChannel.IceConnectionState : RTCIceConnectionState.@new;

    public RTCPeerConnectionState connectionState { get; private set; } = RTCPeerConnectionState.@new;

    /// <summary>
    /// The certificate being used to negotiate the DTLS handshake with the
    /// remote peer.
    /// </summary>
    //private RTCCertificate _currentCertificate;
    //public RTCCertificate CurrentCertificate
    //{
    //    get
    //    {
    //        return _currentCertificate;
    //    }
    //}

    /// <summary>
    ///     The fingerprint of the certificate being used to negotiate the DTLS handshake with the
    ///     remote peer.
    /// </summary>
    private RTCDtlsFingerprint DtlsCertificateFingerprint { get; }

    /// <summary>
    ///     The SCTP transport over which SCTP data is sent and received.
    /// </summary>
    /// <remarks>
    ///     WebRTC API definition:
    ///     https://www.w3.org/TR/webrtc/#attributes-15
    /// </remarks>
    private RTCSctpTransport sctp { get; }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    ///     The primary stream for this session - can be an AudioStream or a VideoStream
    /// </summary>
    protected MediaStream PrimaryStream { get; private set; }

    /// <summary>
    ///     The primary Audio Stream for this session
    /// </summary>
    private AudioStream AudioStream
    {
        get
        {
            if (AudioStreamList.Count > 0)
            {
                return AudioStreamList[0];
            }

            return null;
        }
    }

    /// <summary>
    ///     The primary Video Stream for this session
    /// </summary>
    private VideoStream VideoStream
    {
        get
        {
            if (VideoStreamList.Count > 0)
            {
                return VideoStreamList[0];
            }

            return null;
        }
    }

    /// <summary>
    ///     List of all Audio Streams for this session
    /// </summary>
    private List<AudioStream> AudioStreamList { get; } = new();

    /// <summary>
    ///     List of all Video Streams for this session
    /// </summary>
    private List<VideoStream> VideoStreamList { get; } = new();

    /// <summary>
    ///     The SDP offered by the remote call party for this session.
    /// </summary>
    protected SDP.SDP RemoteDescription { get; private set; }

    /// <summary>
    ///     Indicates whether the session has been closed. Once a session is closed it cannot
    ///     be restarted.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    ///     Indicates whether the session has been started. Starting a session tells the RTP
    ///     socket to start receiving,
    /// </summary>
    private bool IsStarted { get; set; }

    /// <summary>
    ///     Indicates whether this session is using audio.
    /// </summary>
    private bool HasAudio => AudioStream?.HasAudio == true;

    /// <summary>
    ///     Indicates whether this session is using video.
    /// </summary>
    private bool HasVideo => VideoStream?.HasVideo == true;

    /// <summary>
    ///     Close the session if the instance is out of scope.
    /// </summary>
    public void Dispose()
    {
        Close("disposed");
    }

    /// <summary>
    ///     Informs the application that session negotiation needs to be done (i.e. a CreateOffer call
    ///     followed by setLocalDescription).
    /// </summary>
    public event Action onnegotiationneeded;

    private event Action<RTCIceCandidate> _onIceCandidate;

    /// <summary>
    ///     A new ICE candidate is available for the Peer Connection.
    /// </summary>
    public event Action<RTCIceCandidate> onicecandidate
    {
        add
        {
            var notifyIce = _onIceCandidate == null && value != null;
            _onIceCandidate += value;
            if (notifyIce)
            {
                foreach (var ice in RtpIceChannel.Candidates)
                {
                    _onIceCandidate?.Invoke(ice);
                }
            }
        }
        remove => _onIceCandidate -= value;
    }

    /// <summary>
    ///     A failure occurred when gathering ICE candidates.
    /// </summary>
    public event Action<RTCIceCandidate, string> onicecandidateerror;

    /// <summary>
    ///     The signaling state has changed. This state change is the result of either setLocalDescription or
    ///     setRemoteDescription being invoked.
    /// </summary>
    public event Action onsignalingstatechange;

    /// <summary>
    ///     This Peer Connection's ICE connection state has changed.
    /// </summary>
    public event Action<RTCIceConnectionState> oniceconnectionstatechange;

    /// <summary>
    ///     This Peer Connection's ICE gathering state has changed.
    /// </summary>
    public event Action<RTCIceGatheringState> onicegatheringstatechange;

    /// <summary>
    ///     The state of the peer connection. A state of connected means the ICE checks have
    ///     succeeded and the DTLS handshake has completed. Once in the connected state it's
    ///     suitable for media packets can be exchanged.
    /// </summary>
    public event Action<RTCPeerConnectionState> onconnectionstatechange;

    /// <summary>
    ///     Event handler for ICE connection state changes.
    /// </summary>
    /// <param name="state">The new ICE connection state.</param>
    private async void IceConnectionStateChange(RTCIceConnectionState iceState)
    {
        oniceconnectionstatechange?.Invoke(iceConnectionState);

        if (iceState == RTCIceConnectionState.connected && RtpIceChannel.NominatedEntry != null)
        {
            if (_dtlsHandle != null)
            {
                if (PrimaryStream.DestinationEndPoint?.Address.Equals(RtpIceChannel.NominatedEntry.RemoteCandidate
                        .DestinationEndPoint.Address) == false ||
                    PrimaryStream.DestinationEndPoint?.Port !=
                    RtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint.Port)
                {
                    // Already connected and this event is due to change in the nominated remote candidate.
                    var connectedEP = RtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint;

                    SetGlobalDestination(connectedEP, connectedEP);
                    Logger.LogInformation($"ICE changing connected remote end point to {connectedEP}.");
                }

                if (connectionState == RTCPeerConnectionState.disconnected ||
                    connectionState == RTCPeerConnectionState.failed)
                {
                    // The ICE connection state change is due to a re-connection.
                    connectionState = RTCPeerConnectionState.connected;
                    onconnectionstatechange?.Invoke(connectionState);
                }
            }
            else
            {
                connectionState = RTCPeerConnectionState.connecting;
                onconnectionstatechange?.Invoke(connectionState);

                var connectedEP = RtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint;

                SetGlobalDestination(connectedEP, connectedEP);
                Logger.LogInformation($"ICE connected to remote end point {connectedEP}.");

                if (IceRole == IceRolesEnum.active)
                {
                    _dtlsHandle = new DtlsSrtpTransport(new DtlsSrtpClient(_dtlsCertificate, _dtlsPrivateKey)
                        { ForceUseExtendedMasterSecret = true });
                }
                else
                {
                    _dtlsHandle = new DtlsSrtpTransport(new DtlsSrtpServer(_dtlsCertificate, _dtlsPrivateKey)
                        { ForceUseExtendedMasterSecret = true });
                }

                _dtlsHandle.OnAlert += OnDtlsAlert;

                Logger.LogDebug($"Starting DLS handshake with role {IceRole}.");

                try
                {
                    var handshakeResult = await Task.Run(() => DoDtlsHandshake(_dtlsHandle)).ConfigureAwait(false);

                    connectionState = handshakeResult
                        ? RTCPeerConnectionState.connected
                        : connectionState = RTCPeerConnectionState.failed;
                    onconnectionstatechange?.Invoke(connectionState);

                    if (connectionState == RTCPeerConnectionState.connected)
                    {
                        await Start().ConfigureAwait(false);
                        await InitialiseSctpTransport().ConfigureAwait(false);
                    }
                }
                catch (Exception excp)
                {
                    Logger.LogWarning(excp, $"RTCPeerConnection DTLS handshake failed. {excp.Message}");

                    //connectionState = RTCPeerConnectionState.failed;
                    //onconnectionstatechange?.Invoke(connectionState);

                    Close("dtls handshake failed");
                }
            }
        }

        if (iceConnectionState == RTCIceConnectionState.checking)
        {
            // Not sure about this correspondence between the ICE and peer connection states.
            // TODO: Double check spec.
            //connectionState = RTCPeerConnectionState.connecting;
            //onconnectionstatechange?.Invoke(connectionState);
        }
        else if (iceConnectionState == RTCIceConnectionState.disconnected)
        {
            if (connectionState == RTCPeerConnectionState.connected)
            {
                connectionState = RTCPeerConnectionState.disconnected;
                onconnectionstatechange?.Invoke(connectionState);
            }
            else
            {
                connectionState = RTCPeerConnectionState.failed;
                onconnectionstatechange?.Invoke(connectionState);
            }
        }
        else if (iceConnectionState == RTCIceConnectionState.failed)
        {
            connectionState = RTCPeerConnectionState.failed;
            onconnectionstatechange?.Invoke(connectionState);
        }
    }

    /// <summary>
    ///     Creates a new RTP ICE channel (which manages the UDP socket sending and receiving RTP
    ///     packets) for use with this session.
    /// </summary>
    /// <returns>A new RTPChannel instance.</returns>
    protected RTPChannel CreateRtpChannel()
    {
        if (RtpSessionConfig.IsMediaMultiplexed)
        {
            if (MultiplexRtpChannel != null)
            {
                return MultiplexRtpChannel;
            }
        }

        var rtpIceChannel = new RtpIceChannel(
            RTCIceTransportPolicy.all,
            false,
            RtpSessionConfig.BindPort == 0 ? 0 : RtpSessionConfig.BindPort + MRtpChannelsCount * 2 + 2);

        if (RtpSessionConfig.IsMediaMultiplexed)
        {
            MultiplexRtpChannel = rtpIceChannel;
        }

        rtpIceChannel.OnRTPDataReceived += OnRTPDataReceived;

        // Start the RTP, and if required the Control, socket receivers and the RTCP session.
        rtpIceChannel.Start();

        MRtpChannelsCount++;

        return rtpIceChannel;
    }

    /// <summary>
    ///     Updates the session after receiving the remote SDP.
    /// </summary>
    /// <param name="init">The answer/offer SDP from the remote party.</param>
    public SetDescriptionResultEnum setRemoteDescription(RTCSessionDescriptionInit init)
    {
        remoteDescription = new RTCSessionDescription { type = init.type, sdp = SDP.SDP.ParseSDPDescription(init.sdp) };

        var remoteSdp = remoteDescription.sdp; // SDP.ParseSDPDescription(init.sdp);

        var sdpType = init.type == RTCSdpType.offer ? SdpType.offer : SdpType.answer;

        switch (signalingState)
        {
            case var sigState when sigState == RTCSignalingState.have_local_offer && sdpType == SdpType.offer:
                Logger.LogWarning(
                    $"RTCPeerConnection received an SDP offer but was already in {sigState} state. Remote offer rejected.");
                return SetDescriptionResultEnum.WrongSdpTypeOfferAfterOffer;
        }

        var setResult = SetRemoteDescription(sdpType, remoteSdp);

        if (setResult == SetDescriptionResultEnum.OK)
        {
            var remoteIceUser = remoteSdp.IceUfrag;
            var remoteIcePassword = remoteSdp.IcePwd;
            var dtlsFingerprint = remoteSdp.DtlsFingerprint;
            var remoteIceRole = remoteSdp.IceRole;

            foreach (var ann in remoteSdp.Media)
            {
                if (remoteIceUser == null || remoteIcePassword == null || dtlsFingerprint == null ||
                    remoteIceRole == null)
                {
                    remoteIceUser = remoteIceUser ?? ann.IceUfrag;
                    remoteIcePassword = remoteIcePassword ?? ann.IcePwd;
                    dtlsFingerprint = dtlsFingerprint ?? ann.DtlsFingerprint;
                    remoteIceRole = remoteIceRole ?? ann.IceRole;
                }

                // Check for data channel announcements.
                if (ann.Media == SDPMediaTypesEnum.application &&
                    ann.MediaFormats.Count() == 1 &&
                    ann.ApplicationMediaFormats.Single().Key == SDP_DATACHANNEL_FORMAT_ID)
                {
                    if (ann.Transport == RTP_MEDIA_DATACHANNEL_DTLS_PROFILE ||
                        ann.Transport == RTP_MEDIA_DATACHANNEL_UDPDTLS_PROFILE)
                    {
                        dtlsFingerprint = dtlsFingerprint ?? ann.DtlsFingerprint;
                        remoteIceRole = remoteIceRole ?? remoteSdp.IceRole;
                    }
                    else
                    {
                        Logger.LogWarning(
                            $"The remote SDP requested an unsupported data channel transport of {ann.Transport}.");
                        return SetDescriptionResultEnum.DataChannelTransportNotSupported;
                    }
                }
            }

            if (remoteSdp.IceImplementation == IceImplementationEnum.lite)
            {
                RtpIceChannel.IsController = true;
            }

            if (init.type == RTCSdpType.answer)
            {
                RtpIceChannel.IsController = true;
                IceRole = remoteIceRole == IceRolesEnum.passive ? IceRolesEnum.active : IceRolesEnum.passive;
            }
            //As Chrome does not support changing IceRole while renegotiating we need to keep same previous IceRole if we already negotiated before
            else
            {
                // Set DTLS role as client.
                IceRole = IceRolesEnum.active;
            }

            if (remoteIceUser != null && remoteIcePassword != null)
            {
                RtpIceChannel.SetRemoteCredentials(remoteIceUser, remoteIcePassword);
            }

            if (!string.IsNullOrWhiteSpace(dtlsFingerprint))
            {
                dtlsFingerprint = dtlsFingerprint.Trim().ToLower();
                if (RTCDtlsFingerprint.TryParse(dtlsFingerprint, out var remoteFingerprint))
                {
                    RemotePeerDtlsFingerprint = remoteFingerprint;
                }
                else
                {
                    Logger.LogWarning("The DTLS fingerprint was invalid or not supported.");
                    return SetDescriptionResultEnum.DtlsFingerprintDigestNotSupported;
                }
            }
            else
            {
                Logger.LogWarning("The DTLS fingerprint was missing from the remote party's session description.");
                return SetDescriptionResultEnum.DtlsFingerprintMissing;
            }

            // All browsers seem to have gone to trickling ICE candidates now but just
            // in case one or more are given we can start the STUN dance immediately.
            if (remoteSdp.IceCandidates != null)
            {
                foreach (var iceCandidate in remoteSdp.IceCandidates)
                {
                    AddIceCandidate(new RTCIceCandidateInit { candidate = iceCandidate });
                }
            }


            ResetRemoteSDPSsrcAttributes();
            foreach (var media in remoteSdp.Media)
            {
                if (media.IceCandidates != null)
                {
                    foreach (var iceCandidate in media.IceCandidates)
                    {
                        AddIceCandidate(new RTCIceCandidateInit { candidate = iceCandidate });
                    }
                }

                AddRemoteSDPSsrcAttributes(media.Media, media.SsrcAttributes);
            }

            Logger.LogDebug($"SDP:[{remoteSdp}]");
            LogRemoteSDPSsrcAttributes();


            UpdatedSctpDestinationPort();

            if (init.type == RTCSdpType.offer)
            {
                signalingState = RTCSignalingState.have_remote_offer;
                onsignalingstatechange?.Invoke();
            }
            else
            {
                signalingState = RTCSignalingState.stable;
                onsignalingstatechange?.Invoke();
            }

            // Trigger the ICE candidate events for any non-host candidates, host candidates are always included in the
            // SDP offer/answer. The reason for the trigger is that ICE candidates cannot be sent to the remote peer
            // until it is ready to receive them which is indicated by the remote offer being received.
            foreach (var nonHostCand in RtpIceChannel.Candidates.Where(x => x.type != RTCIceCandidateType.host))
            {
                _onIceCandidate?.Invoke(nonHostCand);
            }
        }

        return setResult;
    }

    /// <summary>
    ///     Close the session including the underlying RTP session and channels.
    /// </summary>
    /// <param name="reason">An optional descriptive reason for the closure.</param>
    public void Close(string reason)
    {
        if (!IsClosed)
        {
            Logger.LogDebug($"Peer connection closed with reason {(reason != null ? reason : "<none>")}.");

            RtpIceChannel?.Close();
            _dtlsHandle?.Close();

            if (sctp != null && sctp.state == RTCSctpTransportState.Connected)
            {
                sctp?.Close();
            }

            if (!IsClosed)
            {
                IsClosed = true;

                foreach (var audioStream in AudioStreamList)
                {
                    if (audioStream != null)
                    {
                        audioStream.IsClosed = true;
                        CloseRtcpSession(audioStream, reason);

                        if (audioStream.HasRtpChannel())
                        {
                            var rtpChannel = audioStream.RTPChannel;
                            rtpChannel.OnRTPDataReceived -= OnReceive;
                            rtpChannel.OnClosed -= OnRTPChannelClosed;
                            rtpChannel.Close(reason);
                        }
                    }
                }

                foreach (var videoStream in VideoStreamList)
                {
                    if (videoStream != null)
                    {
                        videoStream.IsClosed = true;
                        CloseRtcpSession(videoStream, reason);

                        if (videoStream.HasRtpChannel())
                        {
                            var rtpChannel = videoStream.RTPChannel;
                            rtpChannel.OnRTPDataReceived -= OnReceive;
                            rtpChannel.OnClosed -= OnRTPChannelClosed;
                            rtpChannel.Close(reason);
                        }
                    }
                }

                OnRtpClosed?.Invoke(reason);
            }

            connectionState = RTCPeerConnectionState.closed;
            onconnectionstatechange?.Invoke(RTCPeerConnectionState.closed);
        }
    }

    /// <summary>
    ///     Generates the SDP for an offer that can be made to a remote peer.
    /// </summary>
    /// <remarks>
    ///     As specified in https://www.w3.org/TR/webrtc/#dom-rtcpeerconnection-createoffer.
    /// </remarks>
    /// <param name="options">
    ///     Optional. If supplied the options will be sued to apply additional
    ///     controls over the generated offer SDP.
    /// </param>
    public RTCSessionDescriptionInit CreateOffer()
    {
        var mediaStreamList = GetMediaStreams();
        //Revert to DefaultStreamStatus
        foreach (var mediaStream in mediaStreamList)
        {
            if (mediaStream.LocalTrack != null && mediaStream.LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive)
            {
                mediaStream.LocalTrack.StreamStatus = mediaStream.LocalTrack.DefaultStreamStatus;
            }
        }

        var offerSdp = createBaseSdp(mediaStreamList);

        foreach (var ann in offerSdp.Media)
        {
            ann.IceRole = IceRole;
        }

        var initDescription = new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offerSdp.ToString()
        };

        return initDescription;
    }

    /// <summary>
    ///     Gets the RTP channel being used to send and receive data on this peer connection.
    ///     Unlike the base RTP session peer connections only ever use a single RTP channel.
    ///     Audio and video (and RTCP) are all multiplexed on the same channel.
    /// </summary>
    private RtpIceChannel GetRtpChannel()
    {
        return PrimaryStream.RTPChannel as RtpIceChannel;
    }

    /// <summary>
    ///     Generates the base SDP for an offer or answer. The SDP will then be tailored depending
    ///     on whether it's being used in an offer or an answer.
    /// </summary>
    /// <param name="mediaStreamList">THe media streamss to add to the SDP description.</param>
    /// <param name="excludeIceCandidates">
    ///     If true it indicates the caller does not want ICE candidates added
    ///     to the SDP.
    /// </param>
    /// <remarks>
    ///     From https://tools.ietf.org/html/draft-ietf-mmusic-ice-sip-sdp-39#section-4.2.5:
    ///     "The transport address from the peer for the default destination
    ///     is set to IPv4/IPv6 address values "0.0.0.0"/"::" and port value
    ///     of "9".  This MUST NOT be considered as a ICE failure by the peer
    ///     agent and the ICE processing MUST continue as usual."
    /// </remarks>
    private SDP.SDP createBaseSdp(List<MediaStream> mediaStreamList, bool excludeIceCandidates = false)
    {
        // Make sure the ICE gathering of local IP addresses is complete.
        // This task should complete very quickly (<1s) but it is deemed very useful to wait
        // for it to complete as it allows local ICE candidates to be included in the SDP.
        // In theory it would be better to an async/await but that would result in a breaking
        // change to the API and for a one off (once per class instance not once per method call)
        // delay of a few hundred milliseconds it was decided not to break the API.
        _iceGatheringTask.Wait();

        var offerSdp = new SDP.SDP(IPAddress.Loopback);
        offerSdp.SessionId = LocalSdpSessionID;

        var dtlsFingerprint = DtlsCertificateFingerprint.ToString();
        var iceCandidatesAdded = false;


        // Local function to add ICE candidates to one of the media announcements.
        void AddIceCandidates(SDPMediaAnnouncement announcement)
        {
            if (RtpIceChannel.Candidates?.Count > 0)
            {
                announcement.IceCandidates = new List<string>();

                // Add ICE candidates.
                foreach (var iceCandidate in RtpIceChannel.Candidates)
                {
                    announcement.IceCandidates.Add(iceCandidate.ToString());
                }

                foreach (var iceCandidate in _applicationIceCandidates)
                {
                    announcement.IceCandidates.Add(iceCandidate.ToString());
                }

                if (RtpIceChannel.IceGatheringState == RTCIceGatheringState.complete)
                {
                    announcement.AddExtra($"a={SDP.SDP.END_ICE_CANDIDATES_ATTRIBUTE}");
                }
            }
        }

        ;

        // Media announcements must be in the same order in the offer and answer.
        var mediaIndex = 0;
        var audioMediaIndex = 0;
        var videoMediaIndex = 0;
        foreach (var mediaStream in mediaStreamList)
        {
            var mindex = 0;
            var midTag = "0";

            if (RemoteDescription == null)
            {
                mindex = mediaIndex;
                midTag = mediaIndex.ToString();
            }
            else
            {
                if (mediaStream.LocalTrack.Kind == SDPMediaTypesEnum.audio)
                {
                    (mindex, midTag) =
                        RemoteDescription.GetIndexForMediaType(mediaStream.LocalTrack.Kind, audioMediaIndex);
                    audioMediaIndex++;
                }
                else if (mediaStream.LocalTrack.Kind == SDPMediaTypesEnum.video)
                {
                    (mindex, midTag) =
                        RemoteDescription.GetIndexForMediaType(mediaStream.LocalTrack.Kind, videoMediaIndex);
                    videoMediaIndex++;
                }
            }

            mediaIndex++;

            if (mindex == SDP.SDP.MEDIA_INDEX_NOT_PRESENT)
            {
                Logger.LogWarning(
                    $"Media announcement for {mediaStream.LocalTrack.Kind} omitted due to no reciprocal remote announcement.");
            }
            else
            {
                var announcement = new SDPMediaAnnouncement(
                    mediaStream.LocalTrack.Kind,
                    SDP.SDP.IGNORE_RTP_PORT_NUMBER,
                    mediaStream.LocalTrack.Capabilities);

                announcement.Transport = RTP_MEDIA_PROFILE;
                announcement.Connection = new SDPConnectionInformation(IPAddress.Any);
                announcement.AddExtra(RTCP_MUX_ATTRIBUTE);
                announcement.AddExtra(RTCP_ATTRIBUTE);
                announcement.MediaStreamStatus = mediaStream.LocalTrack.StreamStatus;
                announcement.MediaID = midTag;
                announcement.MLineIndex = mindex;

                announcement.IceUfrag = RtpIceChannel.LocalIceUser;
                announcement.IcePwd = RtpIceChannel.LocalIcePassword;
                announcement.IceOptions = ICE_OPTIONS;
                announcement.IceRole = IceRole;
                announcement.DtlsFingerprint = dtlsFingerprint;

                if (iceCandidatesAdded == false && !excludeIceCandidates)
                {
                    AddIceCandidates(announcement);
                    iceCandidatesAdded = true;
                }

                if (mediaStream.LocalTrack.Ssrc != 0)
                {
                    var trackCname = mediaStream.RtcpSession?.Cname;

                    if (trackCname != null)
                    {
                        announcement.SsrcAttributes.Add(new SDPSsrcAttribute(mediaStream.LocalTrack.Ssrc, trackCname,
                            null));
                    }
                }

                offerSdp.Media.Add(announcement);
            }
        }

        if (DataChannels.Count > 0 ||
            (RemoteDescription?.Media.Any(x => x.Media == SDPMediaTypesEnum.application) ?? false))
        {
            int mindex;
            string midTag;
            if (RemoteDescription == null)
            {
                (mindex, midTag) = (mediaIndex, mediaIndex.ToString());
            }
            else
            {
                (mindex, midTag) = RemoteDescription.GetIndexForMediaType(SDPMediaTypesEnum.application, 0);
            }

            if (mindex == SDP.SDP.MEDIA_INDEX_NOT_PRESENT)
            {
                Logger.LogWarning(
                    "Media announcement for data channel establishment omitted due to no reciprocal remote announcement.");
            }
            else
            {
                var dataChannelAnnouncement = new SDPMediaAnnouncement(
                    SDPMediaTypesEnum.application,
                    SDP.SDP.IGNORE_RTP_PORT_NUMBER,
                    new List<SDPApplicationMediaFormat> { new(SDP_DATACHANNEL_FORMAT_ID) });
                dataChannelAnnouncement.Transport = RTP_MEDIA_DATACHANNEL_UDPDTLS_PROFILE;
                dataChannelAnnouncement.Connection = new SDPConnectionInformation(IPAddress.Any);

                dataChannelAnnouncement.SctpPort = SCTP_DEFAULT_PORT;
                dataChannelAnnouncement.MaxMessageSize = sctp.maxMessageSize;
                dataChannelAnnouncement.MLineIndex = mindex;
                dataChannelAnnouncement.MediaID = midTag;
                dataChannelAnnouncement.IceUfrag = RtpIceChannel.LocalIceUser;
                dataChannelAnnouncement.IcePwd = RtpIceChannel.LocalIcePassword;
                dataChannelAnnouncement.IceOptions = ICE_OPTIONS;
                dataChannelAnnouncement.IceRole = IceRole;
                dataChannelAnnouncement.DtlsFingerprint = dtlsFingerprint;

                if (iceCandidatesAdded == false && !excludeIceCandidates)
                {
                    AddIceCandidates(dataChannelAnnouncement);
                    iceCandidatesAdded = true;
                }

                offerSdp.Media.Add(dataChannelAnnouncement);
            }
        }

        // Set the Bundle attribute to indicate all media announcements are being multiplexed.
        if (offerSdp.Media?.Count > 0)
        {
            offerSdp.Group = BUNDLE_ATTRIBUTE;
            foreach (var ann in offerSdp.Media.OrderBy(x => x.MLineIndex).ThenBy(x => x.MediaID))
            {
                offerSdp.Group += $" {ann.MediaID}";
            }
        }

        return offerSdp;
    }

    /// <summary>
    ///     From RFC5764:
    ///     +----------------+
    ///     | 127 < B< 192  -+--> forward to RTP
    ///     |                |
    ///     packet -->  |  19 < B< 64   -+--> forward to DTLS
    ///     |                |
    ///     |       B< 2    -+--> forward to STUN
    ///     +----------------+
    /// </summary>
    /// <paramref name="localPort">The local port on the RTP socket that received the packet.</paramref>
    /// <param name="remoteEP">The remote end point the packet was received from.</param>
    /// <param name="buffer">The data received.</param>
    private void OnRTPDataReceived(int localPort, IPEndPoint remoteEP, byte[] buffer)
    {
        //logger.LogDebug($"RTP channel received a packet from {remoteEP}, {buffer?.Length} bytes.");

        // By this point the RTP ICE channel has already processed any STUN packets which means
        // it's only necessary to separate RTP/RTCP from DTLS.
        // Because DTLS packets can be fragmented and RTP/RTCP should never be use the RTP/RTCP
        // prefix to distinguish.

        if (buffer?.Length > 0)
        {
            try
            {
                if (buffer?.Length > RtpHeader.MIN_HEADER_LEN && buffer[0] >= 128 && buffer[0] <= 191)
                {
                    // RTP/RTCP packet.
                    OnReceive(localPort, remoteEP, buffer);
                }
                else
                {
                    if (_dtlsHandle != null)
                    {
                        //logger.LogDebug($"DTLS transport received {buffer.Length} bytes from {AudioDestinationEndPoint}.");
                        _dtlsHandle.WriteToRecvStream(buffer);
                    }
                    else
                    {
                        Logger.LogWarning(
                            $"DTLS packet received {buffer.Length} bytes from {remoteEP} but no DTLS transport available.");
                    }
                }
            }
            catch (Exception excp)
            {
                Logger.LogError($"Exception RTCPeerConnection.OnRTPDataReceived {excp.Message}");
            }
        }
    }

    /// <summary>
    ///     Used to add remote ICE candidates to the peer connection's checklist.
    /// </summary>
    /// <param name="candidateInit">The remote ICE candidate to add.</param>
    private void AddIceCandidate(RTCIceCandidateInit candidateInit)
    {
        var candidate = new RTCIceCandidate(candidateInit);

        if (RtpIceChannel.Component == candidate.component)
        {
            RtpIceChannel.AddRemoteCandidate(candidate);
        }
        else
        {
            Logger.LogWarning(
                $"Remote ICE candidate not added as no available ICE session for component {candidate.component}.");
        }
    }

    /// <summary>
    ///     Once the SDP exchange has been made the SCTP transport ports are known. If the destination
    ///     port is not using the default value attempt to update it on teh SCTP transprot.
    /// </summary>
    private void UpdatedSctpDestinationPort()
    {
        // If a data channel was requested by the application then create the SCTP association.
        var sctpAnn = RemoteDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.application).FirstOrDefault();
        var destinationPort = sctpAnn?.SctpPort != null ? sctpAnn.SctpPort.Value : SCTP_DEFAULT_PORT;

        if (destinationPort != SCTP_DEFAULT_PORT)
        {
            sctp.UpdateDestinationPort(destinationPort);
        }
    }

    /// <summary>
    ///     Cancel current Negotiation Event Call to prevent running thread to call OnNegotiationNeeded
    /// </summary>
    private void CancelOnNegotiationNeededTask()
    {
        lock (_renegotiationLock)
        {
            if (_cancellationSource != null)
            {
                if (!_cancellationSource.IsCancellationRequested)
                {
                    _cancellationSource.Cancel();
                }

                _cancellationSource = null;
            }
        }
    }

    /// <summary>
    ///     Initialises the SCTP transport. This will result in the DTLS SCTP transport listening
    ///     for incoming INIT packets if the remote peer attempts to create the association. The local
    ///     peer will NOT attempt to establish the association at this point. It's up to the
    ///     application to specify it wants a data channel to initiate the SCTP association attempt.
    /// </summary>
    private async Task InitialiseSctpTransport()
    {
        try
        {
            sctp.OnStateChanged += OnSctpTransportStateChanged;
            sctp.Start(_dtlsHandle.Transport, _dtlsHandle.IsClient);

            if (DataChannels.Count > 0)
            {
                await InitialiseSctpAssociation().ConfigureAwait(false);
            }
        }
        catch (Exception excp)
        {
            Logger.LogError($"SCTP exception establishing association, data channels will not be available. {excp}");
            sctp?.Close();
        }
    }

    /// <summary>
    ///     Event handler for changes to the SCTP transport state.
    /// </summary>
    /// <param name="state">The new transport state.</param>
    private void OnSctpTransportStateChanged(RTCSctpTransportState state)
    {
        if (state == RTCSctpTransportState.Connected)
        {
            Logger.LogDebug("SCTP transport successfully connected.");

            sctp.RTCSctpAssociation.OnDataChannelOpened += OnSctpAssociationDataChannelOpened;
            sctp.RTCSctpAssociation.OnNewDataChannel += OnSctpAssociationNewDataChannel;

            // Create new SCTP streams for any outstanding data channel requests.
            foreach (var dataChannel in dataChannels.ActivatePendingChannels())
            {
                OpenDataChannel(dataChannel);
            }
        }
    }

    /// <summary>
    ///     Event handler for a new data channel being opened by the remote peer.
    /// </summary>
    private void OnSctpAssociationNewDataChannel(ushort streamID, DataChannelTypes type, ushort priority,
        uint reliability, string label, string protocol)
    {
        Logger.LogInformation($"WebRTC new data channel opened by remote peer for stream ID {streamID}, type {type}, " +
                              $"priority {priority}, reliability {reliability}, label {label}, protocol {protocol}.");

        // TODO: Set reliability, priority etc. properties on the data channel.
        var dc = new RTCDataChannel(sctp)
            { id = streamID, label = label, IsOpened = true, readyState = RTCDataChannelState.open };

        dc.SendDcepAck();

        if (dataChannels.AddActiveChannel(dc))
        {
        }
        else
        {
            // TODO: What's the correct behaviour here?? I guess use the newest one and remove the old one?
            Logger.LogWarning($"WebRTC duplicate data channel requested for stream ID {streamID}.");
        }
    }

    /// <summary>
    ///     Event handler for the confirmation that a data channel opened by this peer has been acknowledged.
    /// </summary>
    /// <param name="streamID">The ID of the stream corresponding to the acknowledged data channel.</param>
    private void OnSctpAssociationDataChannelOpened(ushort streamID)
    {
        dataChannels.TryGetChannel(streamID, out var dc);

        var label = dc != null ? dc.label : "<none>";
        Logger.LogInformation($"WebRTC data channel opened label {label} and stream ID {streamID}.");

        if (dc != null)
        {
            dc.GotAck();
        }
        else
        {
            Logger.LogWarning($"WebRTC data channel got ACK but data channel not found for stream ID {streamID}.");
        }
    }

    /// <summary>
    ///     When a data channel is requested an SCTP association is needed. This method attempts to
    ///     initialise the association if it is not already available.
    /// </summary>
    private async Task InitialiseSctpAssociation()
    {
        if (sctp.RTCSctpAssociation.State != SctpAssociationState.Established)
        {
            sctp.Associate();
        }

        if (sctp.state != RTCSctpTransportState.Connected)
        {
            var onSctpConnectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            sctp.OnStateChanged += state =>
            {
                Logger.LogDebug($"SCTP transport for create data channel request changed to state {state}.");

                if (state == RTCSctpTransportState.Connected)
                {
                    onSctpConnectedTcs.TrySetResult(true);
                }
            };

            var startTime = DateTime.Now;

            var completedTask = await Task
                .WhenAny(onSctpConnectedTcs.Task, Task.Delay(SCTP_ASSOCIATE_TIMEOUT_SECONDS * 1000))
                .ConfigureAwait(false);

            if (sctp.state != RTCSctpTransportState.Connected)
            {
                var duration = DateTime.Now.Subtract(startTime).TotalMilliseconds;

                if (completedTask != onSctpConnectedTcs.Task)
                {
                    throw new ApplicationException(
                        $"SCTP association timed out after {duration:0.##}ms with association in state {sctp.RTCSctpAssociation.State} when attempting to create a data channel.");
                }

                throw new ApplicationException(
                    $"SCTP association failed after {duration:0.##}ms with association in state {sctp.RTCSctpAssociation.State} when attempting to create a data channel.");
            }
        }
    }

    /// <summary>
    ///     Sends the Data Channel Establishment Protocol (DCEP) OPEN message to configure the data
    ///     channel on the remote peer.
    /// </summary>
    /// <param name="dataChannel">The data channel to open.</param>
    private void OpenDataChannel(RTCDataChannel dataChannel)
    {
        if (dataChannel.negotiated)
        {
            Logger.LogDebug(
                $"WebRTC data channel negotiated out of band with label {dataChannel.label} and stream ID {dataChannel.id}; invoking open event");
            dataChannel.GotAck();
        }
        else if (dataChannel.id.HasValue)
        {
            Logger.LogDebug(
                $"WebRTC attempting to open data channel with label {dataChannel.label} and stream ID {dataChannel.id}.");
            dataChannel.SendDcepOpen();
        }
        else
        {
            Logger.LogError("Attempt to open a data channel without an assigned ID has failed.");
        }
    }

    /// <summary>
    ///     DtlsHandshake requires DtlsSrtpTransport to work.
    ///     DtlsSrtpTransport is similar to C++ DTLS class combined with Srtp class and can perform
    ///     Handshake as Server or Client in same call. The constructor of transport require a DtlsStrpClient
    ///     or DtlsSrtpServer to work.
    /// </summary>
    /// <param name="dtlsHandle">The DTLS transport handle to perform the handshake with.</param>
    /// <returns>True if the DTLS handshake is successful or false if not.</returns>
    private bool DoDtlsHandshake(DtlsSrtpTransport dtlsHandle)
    {
        Logger.LogDebug("RTCPeerConnection DoDtlsHandshake started.");

        var rtpChannel = PrimaryStream.RTPChannel;

        dtlsHandle.OnDataReady += buf =>
        {
            //logger.LogDebug($"DTLS transport sending {buf.Length} bytes to {AudioDestinationEndPoint}.");
            rtpChannel.Send(PrimaryStream.DestinationEndPoint, buf);
        };

        var handshakeResult = dtlsHandle.DoHandshake(out var handshakeError);

        if (!handshakeResult)
        {
            handshakeError = handshakeError ?? "unknown";
            Logger.LogWarning($"RTCPeerConnection DTLS handshake failed with error {handshakeError}.");
            Close("dtls handshake failed");
            return false;
        }

        Logger.LogDebug(
            $"RTCPeerConnection DTLS handshake result {true}, is handshake complete {dtlsHandle.IsHandshakeComplete()}.");

        var expectedFp = RemotePeerDtlsFingerprint;
        var remoteFingerprint = DtlsUtils.Fingerprint(expectedFp.algorithm, dtlsHandle.RemoteCertificate);

        if (remoteFingerprint.value?.ToUpper() != expectedFp.value?.ToUpper())
        {
            Logger.LogWarning(
                $"RTCPeerConnection remote certificate fingerprint mismatch, expected {expectedFp}, actual {remoteFingerprint}.");
            Close("dtls fingerprint mismatch");
            return false;
        }

        Logger.LogDebug(
            $"RTCPeerConnection remote certificate fingerprint matched expected value of {remoteFingerprint.value} for {remoteFingerprint.algorithm}.");

        SetGlobalSecurityContext(
            dtlsHandle,
            dtlsHandle.ProtectRtcp,
            dtlsHandle.UnprotectRtcp);

        return true;
    }

    /// <summary>
    ///     Event handler for TLS alerts from the DTLS transport.
    /// </summary>
    /// <param name="alertLevel">The level of the alert: warning or critical.</param>
    /// <param name="alertType">The type of the alert.</param>
    /// <param name="alertDescription">An optional description for the alert.</param>
    private void OnDtlsAlert(AlertLevelsEnum alertLevel, AlertTypesEnum alertType, string alertDescription)
    {
        if (alertType == AlertTypesEnum.CloseNotify)
        {
            Logger.LogDebug("SCTP closing transport as a result of DTLS close notification.");

            // No point keeping the SCTP association open if there is no DTLS transport available.
            sctp?.Close();
        }
        else
        {
            var alertMsg = !string.IsNullOrEmpty(alertDescription) ? $": {alertDescription}" : ".";
            Logger.LogWarning($"DTLS unexpected {alertLevel} alert {alertType}{alertMsg}");
        }
    }

    /// <summary>
    ///     If this session is using a secure context this flag MUST be set to indicate
    ///     the security delegate (SrtpProtect, SrtpUnprotect etc) have been set.
    /// </summary>
    private bool IsSecureContextReady()
    {
        if (HasAudio && !AudioStream.IsSecurityContextReady())
        {
            return false;
        }

        if (HasVideo && !VideoStream.IsSecurityContextReady())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Gets fired when the RTP session and underlying channel are closed.
    /// </summary>
    public event Action<string> OnRtpClosed;

    /// <summary>
    ///     Gets fired when an RTCP BYE packet is received from the remote party.
    ///     The string parameter contains the BYE reason. Normally a BYE
    ///     report means the RTP session is finished. But... cases have been observed where
    ///     an RTCP BYE is received when a remote party is put on hold and then the session
    ///     resumes when take off hold. It's up to the application to decide what action to
    ///     take when n RTCP BYE is received.
    /// </summary>
    public event Action<string> OnRtcpBye;

    /// <summary>
    ///     Gets fired when an RTCP report is received (the primary one). This event is for diagnostics only.
    /// </summary>
    public event Action<IPEndPoint, RtcpCompoundPacket> OnReceiveReport;


    protected void ResetRemoteSDPSsrcAttributes()
    {
        _audioRemoteSdpSsrcAttributes.Clear();
        _videoRemoteSdpSsrcAttributes.Clear();
    }

    protected void AddRemoteSDPSsrcAttributes(SDPMediaTypesEnum mediaType, List<SDPSsrcAttribute> sdpSsrcAttributes)
    {
        if (mediaType == SDPMediaTypesEnum.audio)
        {
            _audioRemoteSdpSsrcAttributes.Add(sdpSsrcAttributes);
        }
        else if (mediaType == SDPMediaTypesEnum.video)
        {
            _videoRemoteSdpSsrcAttributes.Add(sdpSsrcAttributes);
        }
    }

    protected void LogRemoteSDPSsrcAttributes()
    {
        var str = "Audio:[ ";
        foreach (var audioRemoteSdpSsrcAttribute in _audioRemoteSdpSsrcAttributes)
        {
            foreach (var attr in audioRemoteSdpSsrcAttribute)
            {
                str += attr.SSRC + " - ";
            }
        }

        str += "] \r\n Video: [ ";
        foreach (var videoRemoteSdpSsrcAttribute in _videoRemoteSdpSsrcAttributes)
        {
            str += " [";
            foreach (var attr in videoRemoteSdpSsrcAttribute)
            {
                str += attr.SSRC + " - ";
            }

            str += "] ";
        }

        str += " ]";
        Logger.LogDebug($"LogRemoteSDPSsrcAttributes: {str}");
    }

    private void CreateRtcpSession(MediaStream mediaStream)
    {
        if (mediaStream.CreateRtcpSession())
        {
            mediaStream.OnReceiveReportByIndex += RaisedOnOnReceiveReport;
        }
    }

    private void CloseRtcpSession(MediaStream mediaStream, string reason)
    {
        var session = mediaStream.RtcpSession;

        if (session != null)
        {
            mediaStream.OnReceiveReportByIndex -= RaisedOnOnReceiveReport;
            session.Close(reason);
            mediaStream.RtcpSession = null;
        }
    }

    private void RaisedOnOnReceiveReport(int index, IPEndPoint ipEndPoint, RtcpCompoundPacket report)
    {
        if (index == 0)
        {
            OnReceiveReport?.Invoke(ipEndPoint, report);
        }
    }

    private AudioStream GetOrCreateAudioStream(int index)
    {
        if (index < AudioStreamList.Count)
        {
            // We ask too fast a new AudioStram ...
            return AudioStreamList[index];
        }

        if (index == AudioStreamList.Count)
        {
            var audioStream = new AudioStream(RtpSessionConfig, index);
            AudioStreamList.Add(audioStream);
            return audioStream;
        }

        return null;
    }

    private VideoStream GetOrCreateVideoStream(int index)
    {
        if (index < VideoStreamList.Count)
        {
            // We ask too fast a new AudioStram ...
            return VideoStreamList[index];
        }

        if (index == VideoStreamList.Count)
        {
            var videoStream = new VideoStream(RtpSessionConfig, index);
            VideoStreamList.Add(videoStream);
            return videoStream;
        }

        return null;
    }

    /// <summary>
    ///     Sets the remote SDP description for this session.
    /// </summary>
    /// <param name="sdpType">Whether the remote SDP is an offer or answer.</param>
    /// <param name="sessionDescription">The SDP that will be set as the remote description.</param>
    /// <returns>If successful an OK enum result. If not an enum result indicating the failure cause.</returns>
    protected SetDescriptionResultEnum SetRemoteDescription(SdpType sdpType, SDP.SDP sessionDescription)
    {
        if (sessionDescription == null)
        {
            throw new ArgumentNullException(nameof(sessionDescription),
                "The session description cannot be null for SetRemoteDescription.");
        }

        try
        {
            if (sessionDescription.Media?.Count == 0)
            {
                return SetDescriptionResultEnum.NoRemoteMedia;
            }

            if (sessionDescription.Media?.Count == 1)
            {
                var remoteMediaType = sessionDescription.Media.First().Media;
                if (remoteMediaType == SDPMediaTypesEnum.audio &&
                    (AudioStream == null || AudioStream.LocalTrack == null))
                {
                    return SetDescriptionResultEnum.NoMatchingMediaType;
                }

                if (remoteMediaType == SDPMediaTypesEnum.video &&
                    (VideoStream == null || VideoStream.LocalTrack == null))
                {
                    return SetDescriptionResultEnum.NoMatchingMediaType;
                }
            }

            // Pre-flight checks have passed. Move onto matching up the local and remote media streams.
            IPAddress connectionAddress = null;
            if (sessionDescription.Connection != null &&
                !string.IsNullOrEmpty(sessionDescription.Connection.ConnectionAddress))
            {
                connectionAddress = IPAddress.Parse(sessionDescription.Connection.ConnectionAddress);
            }

            var currentAudioStreamCount = 0;
            var currentVideoStreamCount = 0;

            //foreach (var announcement in sessionDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.audio || x.Media == SDPMediaTypesEnum.video))
            foreach (var announcement in sessionDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.video))
            {
                MediaStream currentMediaStream;
                if (announcement.Media == SDPMediaTypesEnum.audio)
                {
                    currentMediaStream = GetOrCreateAudioStream(currentAudioStreamCount++);
                    if (currentMediaStream == null)
                    {
                        return SetDescriptionResultEnum.Error;
                    }
                }
                else
                {
                    currentMediaStream = GetOrCreateVideoStream(currentVideoStreamCount++);
                    if (currentMediaStream == null)
                    {
                        return SetDescriptionResultEnum.Error;
                    }
                }

                var capabilities =
                    // As proved by Azure implementation, we need to send based on capabilities of remote track. Azure return SDP with only one possible Codec (H264 107)
                    // but we receive frames based on our LocalRemoteTracks, so its possiblet o receive a frame with ID 122, for exemple, even when remote annoucement only have 107
                    // Thats why we changed line below to keep local track capabilities untouched as we can always do it during send/receive moment
                    currentMediaStream.LocalTrack?.Capabilities;
                //Keep same order of LocalTrack priority to prevent incorrect sending format
                SDPAudioVideoMediaFormat.SortMediaCapability(capabilities, currentMediaStream.LocalTrack?.Capabilities);

                var remoteRtpEp = GetAnnouncementRTPDestination(announcement, connectionAddress);
                SetLocalTrackStreamStatus(currentMediaStream.LocalTrack, remoteRtpEp);
                IPEndPoint remoteRtcpEp = null;
                if (currentMediaStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
                {
                    remoteRtcpEp = RtpSessionConfig.IsRtcpMultiplexed
                        ? remoteRtpEp
                        : new IPEndPoint(remoteRtpEp.Address, remoteRtpEp.Port + 1);
                }

                currentMediaStream.DestinationEndPoint =
                    remoteRtpEp != null && remoteRtpEp.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER
                        ? remoteRtpEp
                        : currentMediaStream.DestinationEndPoint;
                currentMediaStream.ControlDestinationEndPoint =
                    remoteRtcpEp != null && remoteRtcpEp.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER
                        ? remoteRtcpEp
                        : currentMediaStream.ControlDestinationEndPoint;

                if (currentMediaStream.MediaType == SDPMediaTypesEnum.audio)
                {
                    if (capabilities?.Where(x => x.Name().ToLower() != SDP.SDP.TELEPHONE_EVENT_ATTRIBUTE).Count() == 0)
                    {
                        return SetDescriptionResultEnum.AudioIncompatible;
                    }
                }
                else if (capabilities?.Count == 0 || (currentMediaStream.LocalTrack == null &&
                                                      currentMediaStream.LocalTrack != null &&
                                                      currentMediaStream.LocalTrack.Capabilities?.Count == 0))
                {
                    return SetDescriptionResultEnum.VideoIncompatible;
                }
            }

            //Close old RTCPSessions opened
            foreach (var audioStream in AudioStreamList)
            {
                if (audioStream.RtcpSession != null && audioStream.LocalTrack == null)
                {
                    audioStream.RtcpSession.Close(null);
                }
            }

            //Close old RTCPSessions opened
            foreach (var videoStream in VideoStreamList)
            {
                if (videoStream.RtcpSession != null && videoStream.LocalTrack == null)
                {
                    videoStream.RtcpSession.Close(null);
                }
            }

            // If we get to here then the remote description was compatible with the local media tracks.
            // Set the remote description and end points.
            RemoteDescription = sessionDescription;

            return SetDescriptionResultEnum.OK;
        }
        catch (Exception excp)
        {
            Logger.LogError($"Exception in RTPSession SetRemoteDescription. {excp.Message}.");
            return SetDescriptionResultEnum.Error;
        }
    }

    /// <summary>
    ///     Gets the RTP end point for an SDP media announcement from the remote peer.
    /// </summary>
    /// <param name="announcement">The media announcement to get the connection address for.</param>
    /// <param name="connectionAddress">The remote SDP session level connection address. Will be null if not available.</param>
    /// <returns>An IP end point for an SDP media announcement from the remote peer.</returns>
    private IPEndPoint GetAnnouncementRTPDestination(SDPMediaAnnouncement announcement, IPAddress connectionAddress)
    {
        var kind = announcement.Media;
        IPEndPoint rtpEndPoint = null;

        var remoteAddr = announcement.Connection != null
            ? IPAddress.Parse(announcement.Connection.ConnectionAddress)
            : connectionAddress;

        if (remoteAddr != null)
        {
            if (announcement.Port < IPEndPoint.MinPort || announcement.Port > IPEndPoint.MaxPort)
            {
                Logger.LogWarning($"Remote {kind} announcement contained an invalid port number {announcement.Port}.");

                // Set the remote port number to "9" which means ignore and wait for it be set some other way
                // such as when a remote RTP packet or arrives or ICE negotiation completes.
                rtpEndPoint = new IPEndPoint(remoteAddr, SDP.SDP.IGNORE_RTP_PORT_NUMBER);
            }
            else
            {
                rtpEndPoint = new IPEndPoint(remoteAddr, announcement.Port);
            }
        }

        return rtpEndPoint;
    }

    /// <summary>
    ///     Used for child classes that require a single RTP channel for all RTP (audio and video)
    ///     and RTCP communications.
    /// </summary>
    protected void AddSingleTrack(bool videoAsPrimary)
    {
        if (videoAsPrimary)
        {
            PrimaryStream = GetNextVideoStreamByLocalTrack();
        }
        else
        {
            PrimaryStream = GetNextAudioStreamByLocalTrack();
        }

        InitMediaStream(PrimaryStream);
    }

    private void InitMediaStream(MediaStream currentMediaStream)
    {
        var rtpChannel = CreateRtpChannel();
        currentMediaStream.RTPChannel = rtpChannel;
        CreateRtcpSession(currentMediaStream);
    }

#nullable enable
    /// <summary>
    ///     Adds a media track to this session. A media track represents an audio or video
    ///     stream and can be a local (which means we're sending) or remote (which means
    ///     we're receiving).
    /// </summary>
    /// <param name="track">The media track to add to the session.</param>
    public void AddTrack(MediaStreamTrack track)
    {
        AddLocalTrack(track);
    }
#nullable restore
    /// <summary>
    ///     Adds a local media stream to this session. Local media tracks should be added by the
    ///     application to control what session description offers and answers can be made as
    ///     well as being used to match up with remote tracks.
    /// </summary>
    /// <param name="track">The local track to add.</param>
    private void AddLocalTrack(MediaStreamTrack track)
    {
        MediaStream currentMediaStream;
        if (track.Kind == SDPMediaTypesEnum.audio)
        {
            currentMediaStream = GetNextAudioStreamByLocalTrack();
        }
        else if (track.Kind == SDPMediaTypesEnum.video)
        {
            currentMediaStream = GetNextVideoStreamByLocalTrack();
        }
        else
        {
            return;
        }

        if (track.StreamStatus == MediaStreamStatusEnum.Inactive)
        {
            // Inactive tracks don't use/require any local resources. Instead they are place holders
            // so that the session description offers/answers can be balanced with the remote party.
            // For example if the remote party offers audio and video but we only support audio we
            // can reject the call or we can accept the audio and answer with an inactive video
            // announcement.
            currentMediaStream.LocalTrack = track;
        }
        else
        {
            InitMediaStream(currentMediaStream);
            currentMediaStream.LocalTrack = track;
        }
    }

    protected void SetGlobalDestination(IPEndPoint rtpEndPoint, IPEndPoint rtcpEndPoint)
    {
        foreach (var audioStream in AudioStreamList)
        {
            audioStream.SetDestination(rtpEndPoint, rtcpEndPoint);
        }

        foreach (var videoStream in VideoStreamList)
        {
            videoStream.SetDestination(rtpEndPoint, rtcpEndPoint);
        }
    }

    protected void SetGlobalSecurityContext(DtlsSrtpTransport rtpTransport, ProtectRtpPacket protectRtcp,
        ProtectRtpPacket unprotectRtcp)
    {
        foreach (var audioStream in AudioStreamList)
        {
            audioStream.SetSecurityContext(rtpTransport, protectRtcp, unprotectRtcp);
        }

        foreach (var videoStream in VideoStreamList)
        {
            videoStream.SetSecurityContext(rtpTransport, protectRtcp, unprotectRtcp);
        }
    }

    private void InitIPEndPointAndSecurityContext(MediaStream mediaStream)
    {
        // Get primary AudioStream
        if (PrimaryStream != null && mediaStream != null)
        {
            var secureContext = PrimaryStream.SecurityContext;
            if (secureContext != null)
            {
                mediaStream.SetSecurityContext(secureContext.RtpTransport, secureContext.ProtectRtcpPacket,
                    secureContext.UnprotectRtcpPacket);
            }

            mediaStream.SetDestination(PrimaryStream.DestinationEndPoint, PrimaryStream.ControlDestinationEndPoint);
        }
    }

    private AudioStream GetNextAudioStreamByLocalTrack()
    {
        var index = AudioStreamList.Count;
        if (index > 0)
        {
            foreach (var audioStream in AudioStreamList)
            {
                if (audioStream.LocalTrack == null)
                {
                    return audioStream;
                }
            }
        }

        // We need to create new AudioStream
        var newAudioStream = GetOrCreateAudioStream(index);

        // If it's not the first one we need to init it
        if (index != 0)
        {
            InitIPEndPointAndSecurityContext(newAudioStream);
        }

        return newAudioStream;
    }

    private VideoStream GetNextVideoStreamByLocalTrack()
    {
        var index = VideoStreamList.Count;
        if (index > 0)
        {
            foreach (var videoStream in VideoStreamList)
            {
                if (videoStream.LocalTrack == null)
                {
                    return videoStream;
                }
            }
        }

        // We need to create new VideoStream and Init it
        var newVideoStream = GetOrCreateVideoStream(index);

        InitIPEndPointAndSecurityContext(newVideoStream);
        return newVideoStream;
    }

    /// <summary>
    ///     Adjust the stream status of the local media tracks based on the remote tracks.
    /// </summary>
    private void SetLocalTrackStreamStatus(MediaStreamTrack localTrack, IPEndPoint remoteRTPEndPoint)
    {
        if (localTrack != null)
        {
            if (localTrack.StreamStatus == MediaStreamStatusEnum.Inactive)
            {
                localTrack.StreamStatus = localTrack.DefaultStreamStatus;
            }

            if (remoteRTPEndPoint != null)
            {
                if (IPAddress.Any.Equals(remoteRTPEndPoint.Address) ||
                    IPAddress.IPv6Any.Equals(remoteRTPEndPoint.Address))
                {
                    // A connection address of 0.0.0.0 or [::], which is unreachable, means the media is inactive, except
                    // if a special port number is used (defined as "9") which indicates that the media announcement is not
                    // responsible for setting the remote end point for the audio stream. Instead it's most likely being set
                    // using ICE.
                    if (remoteRTPEndPoint.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER)
                    {
                        localTrack.StreamStatus = MediaStreamStatusEnum.Inactive;
                    }
                }
                else if (remoteRTPEndPoint.Port == 0)
                {
                    localTrack.StreamStatus = MediaStreamStatusEnum.Inactive;
                }
            }
        }
    }

    /// <summary>
    ///     Gets the media streams available in this session. Will only be audio, video or both.
    ///     media streams represent an audio or video source that we are sending to the remote party.
    /// </summary>
    /// <returns>A list of the local tracks that have been added to this session.</returns>
    protected List<MediaStream> GetMediaStreams()
    {
        var mediaStream = new List<MediaStream>();

        foreach (var audioStream in AudioStreamList)
        {
            if (audioStream.LocalTrack != null)
            {
                mediaStream.Add(audioStream);
            }
        }

        foreach (var videoStream in VideoStreamList)
        {
            if (videoStream.LocalTrack != null)
            {
                mediaStream.Add(videoStream);
            }
        }

        return mediaStream;
    }

    /// <summary>
    ///     Starts the RTCP session(s) that monitor this RTP session.
    /// </summary>
    protected Task Start()
    {
        if (!IsStarted)
        {
            IsStarted = true;


            foreach (var audioStream in AudioStreamList)
            {
                if (audioStream.HasAudio && audioStream.RtcpSession != null &&
                    audioStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
                {
                    // The local audio track may have been disabled if there were no matching capabilities with
                    // the remote party.
                    audioStream.RtcpSession.Start();
                }
            }

            foreach (var videoStream in VideoStreamList)
            {
                if (videoStream.HasVideo && videoStream.RtcpSession != null &&
                    videoStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
                {
                    // The local video track may have been disabled if there were no matching capabilities with
                    // the remote party.
                    videoStream.RtcpSession.Start();
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task SendVideoAsync(RtpPacket packet)
    {
        var vs = VideoStream;
        if (vs != null)
        {
            await vs.SendRtpRawFromPacketAsync(packet);
        }
    }

    protected void OnReceive(int localPort, IPEndPoint remoteEndPoint, byte[] buffer)
    {
        if (remoteEndPoint.Address.IsIPv4MappedToIPv6)
        {
            // Required for matching existing RTP end points (typically set from SDP) and
            // whether or not the destination end point should be switched.
            remoteEndPoint.Address = remoteEndPoint.Address.MapToIPv4();
        }

        // Quick sanity check on whether this is not an RTP or RTCP packet.
        if (buffer?.Length > RtpHeader.MIN_HEADER_LEN && buffer[0] >= 128 && buffer[0] <= 191)
        {
            if ((RtpSessionConfig.IsSecure || RtpSessionConfig.UseSdpCryptoNegotiation) && !IsSecureContextReady())
            {
                Logger.LogWarning("RTP or RTCP packet received before secure context ready.");
            }
            else
            {
                if (Enum.IsDefined(typeof(RtcpReportTypes), buffer[1]))
                {
                    // Only call OnReceiveRTCPPacket for supported RTCPCompoundPacket types
                    if (buffer[1] == (byte)RtcpReportTypes.SR ||
                        buffer[1] == (byte)RtcpReportTypes.RR ||
                        buffer[1] == (byte)RtcpReportTypes.SDES ||
                        buffer[1] == (byte)RtcpReportTypes.BYE ||
                        buffer[1] == (byte)RtcpReportTypes.PSFB ||
                        buffer[1] == (byte)RtcpReportTypes.RTPFB)
                    {
                        OnReceiveRTCPPacket(remoteEndPoint, buffer);
                    }
                }
            }
        }
    }

    private void OnReceiveRTCPPacket(IPEndPoint remoteEndPoint, byte[] buffer)
    {
        // Get the SSRC in order to be able to figure out which media type
        // This will let us choose the apropriate unprotect methods
        var ssrc = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4));

        var mediaStream = GetMediaStream(ssrc);
        if (mediaStream != null)
        {
            var secureContext = mediaStream.SecurityContext;
            if (secureContext != null)
            {
                var res = secureContext.UnprotectRtcpPacket(buffer, buffer.Length, out var outBufLen);
                if (res != 0)
                {
                    Logger.LogWarning($"SRTCP unprotect failed for {mediaStream.MediaType} track, result {res}.");
                    return;
                }

                buffer = buffer.Take(outBufLen).ToArray();
            }
        }
        else
        {
            Logger.LogWarning($"Could not find appropriate remote track for SSRC for RTCP packet - Ssrc:{ssrc}");
        }

        var rtcpPkt = new RtcpCompoundPacket(buffer);
        mediaStream = GetMediaStream(rtcpPkt);
        if (rtcpPkt.Bye != null)
        {
            Logger.LogDebug($"RTCP BYE received for SSRC {rtcpPkt.Bye.Ssrc}, reason {rtcpPkt.Bye.Reason}.");

            // In some cases, such as a SIP re-INVITE, it's possible the RTP session
            // will keep going with a new remote SSRC.
            // We close peer connection only if there is no more local/remote tracks on the primary stream
            if (PrimaryStream.LocalTrack == null)
            {
                OnRtcpBye?.Invoke(rtcpPkt.Bye.Reason);
            }
        }
        else if (!IsClosed)
        {
            if (mediaStream?.RtcpSession != null)
            {
                if (mediaStream.RtcpSession.LastActivityAt == DateTime.MinValue)
                {
                    // On the first received RTCP report for a session check whether the remote end point matches the
                    // expected remote end point. If not it's "likely" that a private IP address was specified in the SDP.
                    // Take the risk and switch the remote control end point to the one we are receiving from.
                    if (mediaStream.ControlDestinationEndPoint == null ||
                        !mediaStream.ControlDestinationEndPoint.Address.Equals(remoteEndPoint.Address) ||
                        mediaStream.ControlDestinationEndPoint.Port != remoteEndPoint.Port)
                    {
                        Logger.LogDebug(
                            $"{mediaStream.MediaType} control end point switched from {mediaStream.ControlDestinationEndPoint} to {remoteEndPoint}.");
                        mediaStream.ControlDestinationEndPoint = remoteEndPoint;
                    }
                }

                mediaStream.RtcpSession.ReportReceived();
                mediaStream.RaiseOnReceiveReportByIndex(remoteEndPoint, rtcpPkt);
            }
            else if (rtcpPkt.ReceiverReport?.Ssrc == RTCP_RR_NOSTREAM_SSRC)
            {
                // Ignore for the time being. Not sure what use an empty RTCP Receiver Report can provide.
            }
            else if (AudioStream?.RtcpSession?.PacketsReceivedCount > 0 ||
                     VideoStream?.RtcpSession?.PacketsReceivedCount > 0)
            {
                // Only give this warning if we've received at least one RTP packet.
                //logger.LogWarning("Could not match an RTCP packet against any SSRC's in the session.");
                //logger.LogTrace(rtcpPkt.GetDebugSummary());
            }
        }
    }

    private MediaStream GetMediaStream(uint ssrc)
    {
        if (HasAudio)
        {
            if (!HasVideo)
            {
                return AudioStream;
            }
        }
        else
        {
            if (HasVideo)
            {
                return VideoStream;
            }
        }

        foreach (var audioStream in AudioStreamList)
        {
            if (audioStream?.LocalTrack?.IsSsrcMatch(ssrc) == true)
            {
                return audioStream;
            }
        }

        foreach (var videoStream in VideoStreamList)
        {
            if (videoStream?.LocalTrack?.IsSsrcMatch(ssrc) == true)
            {
                return videoStream;
            }
        }

        return GetMediaStreamRemoteSDPSsrcAttributes(ssrc);
    }

    private MediaStream GetMediaStreamRemoteSDPSsrcAttributes(uint ssrc)
    {
        if (ssrc < 200)
        {
            return null;
        }

        var found = false;
        int index;

        // Loop au audioRemoteSDPSsrcAttributes
        for (index = 0; index < _audioRemoteSdpSsrcAttributes.Count; index++)
        {
            foreach (var ssrcAttributes in _audioRemoteSdpSsrcAttributes[index])
            {
                if (ssrcAttributes.SSRC == ssrc)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                break;
            }
        }

        // Get related AudioStream if found
        if (found && AudioStreamList.Count > index)
        {
            var audioStream = AudioStreamList[index];
            //if (audioStream?.RemoteTrack != null)
            //{
            //    audioStream.RemoteTrack.Ssrc = ssrc;
            //}
            return audioStream;
        }

        // Loop au videoRemoteSDPSsrcAttributes
        found = false;
        for (index = 0; index < _videoRemoteSdpSsrcAttributes.Count; index++)
        {
            foreach (var ssrcAttributes in _videoRemoteSdpSsrcAttributes[index])
            {
                if (ssrcAttributes.SSRC == ssrc)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                break;
            }
        }

        // Get related VideoStreamList if found
        if (found && VideoStreamList.Count > index)
        {
            var videoStream = VideoStreamList[index];
            //if (videoStream?.RemoteTrack != null)
            //{
            //    videoStream.RemoteTrack.Ssrc = ssrc;
            //}
            return videoStream;
        }

        return null;
    }

    /// <summary>
    ///     Attempts to get MediaStream that matches a received RTCP report.
    /// </summary>
    /// <param name="rtcpPkt">The RTCP compound packet received from the remote party.</param>
    /// <returns>If a match could be found an SSRC the MediaStream otherwise null.</returns>
    private MediaStream GetMediaStream(RtcpCompoundPacket rtcpPkt)
    {
        if (rtcpPkt.SenderReport != null)
        {
            return GetMediaStream(rtcpPkt.SenderReport.Ssrc);
        }

        if (rtcpPkt.ReceiverReport != null)
        {
            return GetMediaStream(rtcpPkt.ReceiverReport.Ssrc);
        }

        if (rtcpPkt.Feedback != null)
        {
            return GetMediaStream(rtcpPkt.Feedback.SenderSsrc);
        }

        // No match on SR/RR SSRC. Check the individual reception reports for a known SSRC.
        List<ReceptionReportSample> receptionReports = null;

        if (rtcpPkt.SenderReport != null)
        {
            receptionReports = rtcpPkt.SenderReport.ReceptionReports;
        }
        else if (rtcpPkt.ReceiverReport != null)
        {
            receptionReports = rtcpPkt.ReceiverReport.ReceptionReports;
        }

        if (receptionReports != null && receptionReports.Count > 0)
        {
            foreach (var recRep in receptionReports)
            {
                var mediaStream = GetMediaStream(recRep.Ssrc);
                if (mediaStream != null)
                {
                    return mediaStream;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Event handler for the RTP channel closure.
    /// </summary>
    private void OnRTPChannelClosed(string reason)
    {
        Close(reason);
    }
}