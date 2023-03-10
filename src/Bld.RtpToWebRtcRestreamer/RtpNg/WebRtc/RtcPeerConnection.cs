using System.Buffers.Binary;
using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Networking;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.WebRtc;

/// <summary>
///     Represents a WebRTC RTCPeerConnection.
/// </summary>
/// <remarks>
///     Interface is defined in https://www.w3.org/TR/webrtc/#interface-definition.
///     The Session Description offer/answer mechanisms are detailed in
///     https://tools.ietf.org/html/rfc8829 "JavaScript Session Establishment Protocol (JSEP)".
/// </remarks>
internal class RtcPeerConnection : IDisposable
{
    // SDP constants.
    //private new const string RTP_MEDIA_PROFILE = "RTP/SAVP";
    private const string RTP_MEDIA_NON_FEEDBACK_PROFILE = "UDP/TLS/RTP/SAVP";
    private const string RTP_MEDIA_DATA_CHANNEL_DTLS_PROFILE = "DTLS/SCTP"; // Legacy.
    private const string RTP_MEDIA_DATA_CHANNEL_UDP_DTLS_PROFILE = "UDP/DTLS/SCTP";
    private const string SDP_DATA_CHANNEL_FORMAT_ID = "webrtc-datachannel";

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
    private const uint RTCP_RR_NO_STREAM_SSRC = 4195875351U;

    private static readonly ILogger Logger = Log.Logger;
    private static readonly string RtcpAttribute = $"a=rtcp:{SDP.IGNORE_RTP_PORT_NUMBER} IN IP4 0.0.0.0";

    private readonly RTCDataChannelCollection _dataChannels;

    private readonly Certificate _dtlsCertificate;

    /// <summary>
    ///     The fingerprint of the certificate being used to negotiate the DTLS handshake with the
    ///     remote peer.
    /// </summary>
    private readonly RTCDtlsFingerprint _dtlsCertificateFingerprint;

    private readonly AsymmetricKeyParameter _dtlsPrivateKey;
    private readonly Task _iceGatheringTask;

    private readonly string _localSdpSessionId;

    [NotNull]
    private readonly MultiplexedRtpChannel _rtpIceChannel;

    private readonly RTCSctpTransport _sctp;
    private readonly List<List<SDPSsrcAttribute>> _videoRemoteSdpSsrcAttributes = new();

    /// <summary>
    ///     List of all Video Streams for this session
    /// </summary>
    [NotNull]
    private readonly VideoStream _videoStream;

    private RTCPeerConnectionState _connectionState = RTCPeerConnectionState.@new;
    private DtlsSrtpTransport DtlsHandle { get; set; } // Looks like need to be property

    /// <summary>
    ///     The ICE role the peer is acting in.
    /// </summary>
    private IceRolesEnum _iceRole = IceRolesEnum.actpass;

    private RTCSessionDescription _remoteDescription;

    /// <summary>
    ///     The DTLS fingerprint supplied by the remote peer in their SDP. Needs to be checked
    ///     that the certificate supplied during the DTLS handshake matches.
    /// </summary>
    private RTCDtlsFingerprint _remotePeerDtlsFingerprint;

    /// <summary>
    ///     The SDP offered by the remote call party for this session.
    /// </summary>
    private SDP _remoteSdp;

    private RTCSignalingState _signalingState = RTCSignalingState.closed;

    /// <summary>
    ///     Constructor to create a new RTC peer connection instance.
    /// </summary>
    public RtcPeerConnection([NotNull] MediaStreamTrack videoTrack)
    {
        _dataChannels = new RTCDataChannelCollection(() => DtlsHandle.IsClient);

        // No certificate was provided so create a new self signed one.
        (_dtlsCertificate, _dtlsPrivateKey) =
            DtlsUtils.CreateSelfSignedTlsCert(ProtocolVersion.DTLSv12, new BcTlsCrypto());

        _dtlsCertificateFingerprint = DtlsUtils.Fingerprint(_dtlsCertificate);

        _localSdpSessionId = Crypto.GetRandomInt(5).ToString();

        _videoStream = new VideoStream(0);

        _rtpIceChannel = new MultiplexedRtpChannel();
        _rtpIceChannel.OnRTPDataReceived += OnRTPDataReceived;
        _rtpIceChannel.Start();
        _videoStream.RTPChannel = _rtpIceChannel;

        if (_videoStream.CreateRtcpSession())
        {
            _videoStream.OnReceiveReportByIndex += RaisedOnOnReceiveReport;
        }

        _rtpIceChannel.OnIceConnectionStateChange += IceConnectionStateChange;

        OnRtpClosed += Close;
        OnRtcpBye += Close;

        _sctp = new RTCSctpTransport(SCTP_DEFAULT_PORT, SCTP_DEFAULT_PORT, _rtpIceChannel.RTPPort);

        // This is the point the ICE session potentially starts contacting STUN and TURN servers.
        // This job was moved to a background thread as it was observed that interacting with the OS network
        // calls and/or initialising DNS was taking up to 600ms, see
        // https://github.com/sipsorcery-org/sipsorcery/issues/456.
        _iceGatheringTask = Task.Run(_rtpIceChannel.StartGathering);

        //---------------Video-----------------
        _videoStream.LocalTrack = videoTrack;
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    ///     Indicates whether the session has been closed. Once a session is closed it cannot
    ///     be restarted.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    ///     Indicates whether the session has been started. Starting a session tells the RTP
    ///     socket to start receiving,
    /// </summary>
    public bool IsStarted { get; set; }
    
    /// <summary>
    ///     Indicates whether this session is using video.
    /// </summary>
    private bool HasVideo => _videoStream.HasVideo;

    /// <summary>
    ///     Close the session if the instance is out of scope.
    /// </summary>
    public void Dispose()
    {
        Close("disposed");
    }

    /// <summary>
    ///     The state of the peer connection. A state of connected means the ICE checks have
    ///     succeeded and the DTLS handshake has completed. Once in the connected state it's
    ///     suitable for media packets can be exchanged.
    /// </summary>
    public event Action<RTCPeerConnectionState> onconnectionstatechange;

    /// <summary>
    ///     Event handler for ICE connection state changes.
    /// </summary>
    private async void IceConnectionStateChange(RTCIceConnectionState iceState)
    {
        if (iceState == RTCIceConnectionState.connected && _rtpIceChannel.NominatedEntry != null)
        {
            if (DtlsHandle != null)
            {
                if (_videoStream.DestinationEndPoint?.Address.Equals(_rtpIceChannel.NominatedEntry.RemoteCandidate
                        .DestinationEndPoint.Address) == false ||
                    _videoStream.DestinationEndPoint?.Port !=
                    _rtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint.Port)
                {
                    // Already connected and this event is due to change in the nominated remote candidate.
                    var connectedEP = _rtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint;

                    SetGlobalDestination(connectedEP, connectedEP);
                    Logger.LogInformation($"ICE changing connected remote end point to {connectedEP}.");
                }

                if (_connectionState == RTCPeerConnectionState.disconnected ||
                    _connectionState == RTCPeerConnectionState.failed)
                {
                    // The ICE connection state change is due to a re-connection.
                    _connectionState = RTCPeerConnectionState.connected;
                    onconnectionstatechange?.Invoke(_connectionState);
                }
            }
            else
            {
                _connectionState = RTCPeerConnectionState.connecting;
                onconnectionstatechange?.Invoke(_connectionState);

                var connectedEP = _rtpIceChannel.NominatedEntry.RemoteCandidate.DestinationEndPoint;

                SetGlobalDestination(connectedEP, connectedEP);
                Logger.LogInformation($"ICE connected to remote end point {connectedEP}.");

                if (_iceRole == IceRolesEnum.active)
                {
                    DtlsHandle = new DtlsSrtpTransport(new DtlsSrtpClient(_dtlsCertificate, _dtlsPrivateKey)
                        { ForceUseExtendedMasterSecret = true });
                }
                else
                {
                    DtlsHandle = new DtlsSrtpTransport(new DtlsSrtpServer(_dtlsCertificate, _dtlsPrivateKey)
                        { ForceUseExtendedMasterSecret = true });
                }

                DtlsHandle.OnAlert += OnDtlsAlert;

                Logger.LogDebug($"Starting DLS handshake with role {_iceRole}.");

                try
                {
                    var handshakeResult = await Task.Run(() => DoDtlsHandshake(DtlsHandle)).ConfigureAwait(false);

                    _connectionState = handshakeResult
                        ? RTCPeerConnectionState.connected
                        : _connectionState = RTCPeerConnectionState.failed;
                    onconnectionstatechange?.Invoke(_connectionState);

                    if (_connectionState == RTCPeerConnectionState.connected)
                    {
                        Start();
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

        if (_rtpIceChannel.IceConnectionState == RTCIceConnectionState.checking)
        {
            // Not sure about this correspondence between the ICE and peer connection states.
            // TODO: Double check spec.
            //connectionState = RTCPeerConnectionState.connecting;
            //onconnectionstatechange?.Invoke(connectionState);
        }
        else if (_rtpIceChannel.IceConnectionState == RTCIceConnectionState.disconnected)
        {
            if (_connectionState == RTCPeerConnectionState.connected)
            {
                _connectionState = RTCPeerConnectionState.disconnected;
                onconnectionstatechange?.Invoke(_connectionState);
            }
            else
            {
                _connectionState = RTCPeerConnectionState.failed;
                onconnectionstatechange?.Invoke(_connectionState);
            }
        }
        else if (_rtpIceChannel.IceConnectionState == RTCIceConnectionState.failed)
        {
            _connectionState = RTCPeerConnectionState.failed;
            onconnectionstatechange?.Invoke(_connectionState);
        }
    }

    /// <summary>
    ///     Updates the session after receiving the remote SDP.
    /// </summary>
    /// <param name="init">The answer/offer SDP from the remote party.</param>
    public SetDescriptionResultEnum SetRemoteDescription(RTCSessionDescriptionInit init)
    {
        _remoteDescription = new RTCSessionDescription
            { type = init.type, sdp = SDP.ParseSDPDescription(init.sdp) };

        var remoteSdp = _remoteDescription.sdp; // SDP.ParseSDPDescription(init.sdp);

        var sdpType = init.type == RTCSdpType.offer ? SdpType.offer : SdpType.answer;

        if (_signalingState == RTCSignalingState.have_local_offer && sdpType == SdpType.offer)
        {
            Logger.LogWarning(
                $"RTCPeerConnection received an SDP offer but was already in {_signalingState} state. Remote offer rejected.");
            return SetDescriptionResultEnum.WrongSdpTypeOfferAfterOffer;
        }

        var setResult = SetRemoteDescription(remoteSdp);

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
                    ann.ApplicationMediaFormats.Single().Key == SDP_DATA_CHANNEL_FORMAT_ID)
                {
                    if (ann.Transport == RTP_MEDIA_DATA_CHANNEL_DTLS_PROFILE ||
                        ann.Transport == RTP_MEDIA_DATA_CHANNEL_UDP_DTLS_PROFILE)
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
                _rtpIceChannel.IsController = true;
            }

            if (init.type == RTCSdpType.answer)
            {
                _rtpIceChannel.IsController = true;
                _iceRole = remoteIceRole == IceRolesEnum.passive ? IceRolesEnum.active : IceRolesEnum.passive;
            }
            //As Chrome does not support changing IceRole while renegotiating we need to keep same previous IceRole if we already negotiated before
            else
            {
                // Set DTLS role as client.
                _iceRole = IceRolesEnum.active;
            }

            if (remoteIceUser != null && remoteIcePassword != null)
            {
                _rtpIceChannel.SetRemoteCredentials(remoteIceUser, remoteIcePassword);
            }

            if (!string.IsNullOrWhiteSpace(dtlsFingerprint))
            {
                dtlsFingerprint = dtlsFingerprint.Trim().ToLower();
                if (RTCDtlsFingerprint.TryParse(dtlsFingerprint, out var remoteFingerprint))
                {
                    _remotePeerDtlsFingerprint = remoteFingerprint;
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


            _videoRemoteSdpSsrcAttributes.Clear();
            foreach (var media in remoteSdp.Media)
            {
                if (media.IceCandidates != null)
                {
                    foreach (var iceCandidate in media.IceCandidates)
                    {
                        AddIceCandidate(new RTCIceCandidateInit { candidate = iceCandidate });
                    }
                }

                if (media.Media == SDPMediaTypesEnum.video)
                {
                    _videoRemoteSdpSsrcAttributes.Add(media.SsrcAttributes);
                }
            }

            Logger.LogDebug($"SDP:[{remoteSdp}]");
            LogRemoteSDPSsrcAttributes();


            UpdatedSctpDestinationPort();

            if (init.type == RTCSdpType.offer)
            {
                _signalingState = RTCSignalingState.have_remote_offer;
            }
            else
            {
                _signalingState = RTCSignalingState.stable;
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

            _rtpIceChannel?.Close();
            DtlsHandle?.Close();

            if (_sctp != null && _sctp.state == RTCSctpTransportState.Connected)
            {
                _sctp?.Close();
            }

            if (!IsClosed)
            {
                IsClosed = true;


                _videoStream.IsClosed = true;
                CloseRtcpSession(_videoStream, reason);

                if (_videoStream.HasRtpChannel())
                {
                    var rtpChannel = _videoStream.RTPChannel;
                    rtpChannel.OnRTPDataReceived -= OnReceive;
                    rtpChannel.OnClosed -= OnRTPChannelClosed;
                    rtpChannel.Close(reason);
                }

                OnRtpClosed?.Invoke(reason);
            }

            _connectionState = RTCPeerConnectionState.closed;
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
        var mediaStreamList = new List<MediaStream>();
        
        if (_videoStream.LocalTrack != null)
        {
            mediaStreamList.Add(_videoStream);
        }

        //Revert to DefaultStreamStatus
        foreach (var mediaStream in mediaStreamList)
        {
            if (mediaStream.LocalTrack != null && mediaStream.LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive)
            {
                mediaStream.LocalTrack.StreamStatus = mediaStream.LocalTrack.DefaultStreamStatus;
            }
        }

        // Make sure the ICE gathering of local IP addresses is complete.
        // This task should complete very quickly (<1s) but it is deemed very useful to wait
        // for it to complete as it allows local ICE candidates to be included in the SDP.
        // In theory it would be better to an async/await but that would result in a breaking
        // change to the API and for a one off (once per class instance not once per method call)
        // delay of a few hundred milliseconds it was decided not to break the API.
        _iceGatheringTask.Wait();

        var offerSdp = new SDP(IPAddress.Loopback)
        {
            SessionId = _localSdpSessionId
        };

        var dtlsFingerprint = _dtlsCertificateFingerprint.ToString();
        var iceCandidatesAdded = false;


        // Local function to add ICE candidates to one of the media announcements.
        void AddIceCandidates(SDPMediaAnnouncement announcement)
        {
            if (_rtpIceChannel.Candidates?.Count > 0)
            {
                announcement.IceCandidates = new List<string>();

                // Add ICE candidates.
                foreach (var iceCandidate in _rtpIceChannel.Candidates)
                {
                    announcement.IceCandidates.Add(iceCandidate.ToString());
                }

                if (_rtpIceChannel.IceGatheringState == RTCIceGatheringState.complete)
                {
                    announcement.AddExtra($"a={SDP.END_ICE_CANDIDATES_ATTRIBUTE}");
                }
            }
        }

        // Media announcements must be in the same order in the offer and answer.
        var mediaIndex = 0;
        var audioMediaIndex = 0;
        var videoMediaIndex = 0;
        foreach (var mediaStream1 in mediaStreamList)
        {
            var mindex = 0;
            var midTag = "0";

            if (_remoteSdp == null)
            {
                mindex = mediaIndex;
                midTag = mediaIndex.ToString();
            }
            else
            {
                if (mediaStream1.LocalTrack.Kind == SDPMediaTypesEnum.audio)
                {
                    (mindex, midTag) =
                        _remoteSdp.GetIndexForMediaType(mediaStream1.LocalTrack.Kind, audioMediaIndex);
                    audioMediaIndex++;
                }
                else if (mediaStream1.LocalTrack.Kind == SDPMediaTypesEnum.video)
                {
                    (mindex, midTag) =
                        _remoteSdp.GetIndexForMediaType(mediaStream1.LocalTrack.Kind, videoMediaIndex);
                    videoMediaIndex++;
                }
            }

            mediaIndex++;

            if (mindex == SDP.MEDIA_INDEX_NOT_PRESENT)
            {
                Logger.LogWarning(
                    $"Media announcement for {mediaStream1.LocalTrack.Kind} omitted due to no reciprocal remote announcement.");
            }
            else
            {
                var announcement = new SDPMediaAnnouncement(
                    mediaStream1.LocalTrack.Kind,
                    SDP.IGNORE_RTP_PORT_NUMBER,
                    mediaStream1.LocalTrack.Capabilities);

                announcement.Transport = RTP_MEDIA_NON_FEEDBACK_PROFILE;
                announcement.Connection = new SDPConnectionInformation(IPAddress.Any);
                announcement.AddExtra(RTCP_MUX_ATTRIBUTE);
                announcement.AddExtra(RtcpAttribute);
                announcement.MediaStreamStatus = mediaStream1.LocalTrack.StreamStatus;
                announcement.MediaID = midTag;
                announcement.MLineIndex = mindex;

                announcement.IceUfrag = _rtpIceChannel.LocalIceUser;
                announcement.IcePwd = _rtpIceChannel.LocalIcePassword;
                announcement.IceOptions = ICE_OPTIONS;
                announcement.IceRole = _iceRole;
                announcement.DtlsFingerprint = dtlsFingerprint;

                if (iceCandidatesAdded == false && !false)
                {
                    AddIceCandidates(announcement);
                    iceCandidatesAdded = true;
                }

                if (mediaStream1.LocalTrack.Ssrc != 0)
                {
                    var trackCname = mediaStream1.RtcpSession?.Cname;

                    if (trackCname != null)
                    {
                        announcement.SsrcAttributes.Add(new SDPSsrcAttribute(mediaStream1.LocalTrack.Ssrc, trackCname,
                            null));
                    }
                }

                offerSdp.Media.Add(announcement);
            }
        }

        if (_dataChannels.Count > 0 ||
            (_remoteSdp?.Media.Any(x => x.Media == SDPMediaTypesEnum.application) ?? false))
        {
            int mindex;
            string midTag;
            if (_remoteSdp == null)
            {
                (mindex, midTag) = (mediaIndex, mediaIndex.ToString());
            }
            else
            {
                (mindex, midTag) = _remoteSdp.GetIndexForMediaType(SDPMediaTypesEnum.application, 0);
            }

            if (mindex == SDP.MEDIA_INDEX_NOT_PRESENT)
            {
                Logger.LogWarning(
                    "Media announcement for data channel establishment omitted due to no reciprocal remote announcement.");
            }
            else
            {
                var dataChannelAnnouncement = new SDPMediaAnnouncement(
                    SDPMediaTypesEnum.application,
                    SDP.IGNORE_RTP_PORT_NUMBER,
                    new List<SDPApplicationMediaFormat> { new(SDP_DATA_CHANNEL_FORMAT_ID) });
                dataChannelAnnouncement.Transport = RTP_MEDIA_DATA_CHANNEL_UDP_DTLS_PROFILE;
                dataChannelAnnouncement.Connection = new SDPConnectionInformation(IPAddress.Any);

                dataChannelAnnouncement.SctpPort = SCTP_DEFAULT_PORT;
                dataChannelAnnouncement.MaxMessageSize = _sctp.maxMessageSize;
                dataChannelAnnouncement.MLineIndex = mindex;
                dataChannelAnnouncement.MediaID = midTag;
                dataChannelAnnouncement.IceUfrag = _rtpIceChannel.LocalIceUser;
                dataChannelAnnouncement.IcePwd = _rtpIceChannel.LocalIcePassword;
                dataChannelAnnouncement.IceOptions = ICE_OPTIONS;
                dataChannelAnnouncement.IceRole = _iceRole;
                dataChannelAnnouncement.DtlsFingerprint = dtlsFingerprint;

                if (iceCandidatesAdded == false && !false)
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
            foreach (var ann1 in offerSdp.Media.OrderBy(x => x.MLineIndex).ThenBy(x => x.MediaID))
            {
                offerSdp.Group += $" {ann1.MediaID}";
            }

            foreach (var ann in offerSdp.Media)
            {
                ann.IceRole = _iceRole;
            }
        }

        return new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offerSdp.ToString()
        };
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
    /// <param name="localPort">The local port on the RTP socket that received the packet.</param>
    /// <param name="remoteEndPoint">The remote end point the packet was received from.</param>
    /// <param name="buffer">The data received.</param>
    private void OnRTPDataReceived(int localPort, IPEndPoint remoteEndPoint, byte[] buffer)
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
                    OnReceive(localPort, remoteEndPoint, buffer);
                }
                else
                {
                    if (DtlsHandle != null)
                    {
                        //logger.LogDebug($"DTLS transport received {buffer.Length} bytes from {AudioDestinationEndPoint}.");
                        DtlsHandle.WriteToRecvStream(buffer);
                    }
                    else
                    {
                        Logger.LogWarning(
                            $"DTLS packet received {buffer.Length} bytes from {remoteEndPoint} but no DTLS transport available.");
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

        if (_rtpIceChannel.Component == candidate.component)
        {
            _rtpIceChannel.AddRemoteCandidate(candidate);
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
        var sctpAnn = _remoteSdp.Media.Where(x => x.Media == SDPMediaTypesEnum.application).FirstOrDefault();
        var destinationPort = sctpAnn?.SctpPort != null ? sctpAnn.SctpPort.Value : SCTP_DEFAULT_PORT;

        if (destinationPort != SCTP_DEFAULT_PORT)
        {
            _sctp.UpdateDestinationPort(destinationPort);
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
            _sctp.OnStateChanged += OnSctpTransportStateChanged;
            _sctp.Start(DtlsHandle.Transport, DtlsHandle.IsClient);

            if (_dataChannels.Count > 0)
            {
                await InitialiseSctpAssociation().ConfigureAwait(false);
            }
        }
        catch (Exception excp)
        {
            Logger.LogError($"SCTP exception establishing association, data channels will not be available. {excp}");
            _sctp?.Close();
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

            _sctp.RTCSctpAssociation.OnDataChannelOpened += OnSctpAssociationDataChannelOpened;
            _sctp.RTCSctpAssociation.OnNewDataChannel += OnSctpAssociationNewDataChannel;

            // Create new SCTP streams for any outstanding data channel requests.
            foreach (var dataChannel in _dataChannels.ActivatePendingChannels())
            {
                OpenDataChannel(dataChannel);
            }
        }
    }

    /// <summary>
    ///     Event handler for a new data channel being opened by the remote peer.
    /// </summary>
    private void OnSctpAssociationNewDataChannel(
        ushort streamId,
        DataChannelTypes type,
        ushort priority,
        uint reliability,
        string label,
        string protocol)
    {
        Logger.LogInformation($"WebRTC new data channel opened by remote peer for stream ID {streamId}, type {type}, " +
                              $"priority {priority}, reliability {reliability}, label {label}, protocol {protocol}.");

        // TODO: Set reliability, priority etc. properties on the data channel.
        var dc = new RTCDataChannel(_sctp)
        {
            id = streamId,
            label = label,
            IsOpened = true,
            readyState = RTCDataChannelState.open
        };

        dc.SendDcepAck();

        if (_dataChannels.AddActiveChannel(dc))
        {
        }
        else
        {
            // TODO: What's the correct behaviour here?? I guess use the newest one and remove the old one?
            Logger.LogWarning($"WebRTC duplicate data channel requested for stream ID {streamId}.");
        }
    }

    /// <summary>
    ///     Event handler for the confirmation that a data channel opened by this peer has been acknowledged.
    /// </summary>
    /// <param name="streamID">The ID of the stream corresponding to the acknowledged data channel.</param>
    private void OnSctpAssociationDataChannelOpened(ushort streamID)
    {
        _dataChannels.TryGetChannel(streamID, out var dc);

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
        if (_sctp.RTCSctpAssociation.State != SctpAssociationState.Established)
        {
            _sctp.Associate();
        }

        if (_sctp.state != RTCSctpTransportState.Connected)
        {
            var onSctpConnectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _sctp.OnStateChanged += state =>
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

            if (_sctp.state != RTCSctpTransportState.Connected)
            {
                var duration = DateTime.Now.Subtract(startTime).TotalMilliseconds;

                if (completedTask != onSctpConnectedTcs.Task)
                {
                    throw new ApplicationException(
                        $"SCTP association timed out after {duration:0.##}ms with association in state {_sctp.RTCSctpAssociation.State} when attempting to create a data channel.");
                }

                throw new ApplicationException(
                    $"SCTP association failed after {duration:0.##}ms with association in state {_sctp.RTCSctpAssociation.State} when attempting to create a data channel.");
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

        var rtpChannel = _videoStream.RTPChannel;

        dtlsHandle.OnDataReady += buf =>
        {
            //logger.LogDebug($"DTLS transport sending {buf.Length} bytes to {AudioDestinationEndPoint}.");
            rtpChannel.Send(_videoStream.DestinationEndPoint, buf);
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

        var expectedFp = _remotePeerDtlsFingerprint;
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
            _sctp?.Close();
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
        if (HasVideo && !_videoStream.IsSecurityContextReady())
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

    private void LogRemoteSDPSsrcAttributes()
    {
        var str = "Video: [ ";
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

    private void CloseRtcpSession(MediaStream mediaStream, string reason)
    {
        if (mediaStream.RtcpSession != null)
        {
            mediaStream.OnReceiveReportByIndex -= RaisedOnOnReceiveReport;
            mediaStream.CloseRtcpSession(reason);
        }
    }

    private void RaisedOnOnReceiveReport(int index, IPEndPoint ipEndPoint, RtcpCompoundPacket report)
    {
        if (index == 0)
        {
            OnReceiveReport?.Invoke(ipEndPoint, report);
        }
    }

    /// <summary>
    ///     Sets the remote SDP description for this session.
    /// </summary>
    /// <param name="sessionDescription">The SDP that will be set as the remote description.</param>
    /// <returns>If successful an OK enum result. If not an enum result indicating the failure cause.</returns>
    private SetDescriptionResultEnum SetRemoteDescription(SDP sessionDescription)
    {
        try
        {
            if (sessionDescription.Media?.Count == 0)
            {
                return SetDescriptionResultEnum.NoRemoteMedia;
            }

            if (sessionDescription.Media?.Count == 1)
            {
                var remoteMediaType = sessionDescription.Media.First().Media;
                
                if (remoteMediaType == SDPMediaTypesEnum.video && _videoStream.LocalTrack == null)
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

            //foreach (var announcement in sessionDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.audio || x.Media == SDPMediaTypesEnum.video))
            foreach (var announcement in sessionDescription.Media.Where(x => x.Media == SDPMediaTypesEnum.video))
            {
                MediaStream currentMediaStream;
                currentMediaStream = _videoStream;

                var capabilities =
                    // As proved by Azure implementation, we need to send based on capabilities of remote track. Azure return SDP with only one possible Codec (H264 107)
                    // but we receive frames based on our LocalRemoteTracks, so its possiblet o receive a frame with ID 122, for exemple, even when remote annoucement only have 107
                    // Thats why we changed line below to keep local track capabilities untouched as we can always do it during send/receive moment
                    currentMediaStream.LocalTrack?.Capabilities;
                //Keep same order of LocalTrack priority to prevent incorrect sending format
                SDPAudioVideoMediaFormat.SortMediaCapability(capabilities, currentMediaStream.LocalTrack?.Capabilities);

                var remoteRtpEp = GetAnnouncementRTPDestination(announcement, connectionAddress);
                if (currentMediaStream.LocalTrack != null)
                {
                    if (currentMediaStream.LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive)
                    {
                        currentMediaStream.LocalTrack.StreamStatus = currentMediaStream.LocalTrack.DefaultStreamStatus;
                    }

                    if (remoteRtpEp != null)
                    {
                        if (IPAddress.Any.Equals(remoteRtpEp.Address) ||
                            IPAddress.IPv6Any.Equals(remoteRtpEp.Address))
                        {
                            // A connection address of 0.0.0.0 or [::], which is unreachable, means the media is inactive, except
                            // if a special port number is used (defined as "9") which indicates that the media announcement is not
                            // responsible for setting the remote end point for the audio stream. Instead it's most likely being set
                            // using ICE.
                            if (remoteRtpEp.Port != SDP.IGNORE_RTP_PORT_NUMBER)
                            {
                                currentMediaStream.LocalTrack.StreamStatus = MediaStreamStatusEnum.Inactive;
                            }
                        }
                        else if (remoteRtpEp.Port == 0)
                        {
                            currentMediaStream.LocalTrack.StreamStatus = MediaStreamStatusEnum.Inactive;
                        }
                    }
                }

                // IPEndPoint remoteRtcpEp = null;
                // if (currentMediaStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
                // {
                //     remoteRtcpEp = _rtpSessionConfig.IsRtcpMultiplexed
                //         ? remoteRtpEp
                //         : new IPEndPoint(remoteRtpEp.Address, remoteRtpEp.Port + 1);
                // }

                // currentMediaStream.DestinationEndPoint =
                //     remoteRtpEp != null && remoteRtpEp.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER
                //         ? remoteRtpEp
                //         : currentMediaStream.DestinationEndPoint;
                // currentMediaStream.ControlDestinationEndPoint =
                //     remoteRtcpEp != null && remoteRtcpEp.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER
                //         ? remoteRtcpEp
                //         : currentMediaStream.ControlDestinationEndPoint;

                if (currentMediaStream.MediaType == SDPMediaTypesEnum.audio)
                {
                    if (capabilities?.Where(x => x.Name().ToLower() != SDP.TELEPHONE_EVENT_ATTRIBUTE).Count() == 0)
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
            
            if (_videoStream.RtcpSession != null && _videoStream.LocalTrack == null)
            {
                _videoStream.RtcpSession.Close(null);
            }

            // If we get to here then the remote description was compatible with the local media tracks.
            // Set the remote description and end points.
            _remoteSdp = sessionDescription;

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
                rtpEndPoint = new IPEndPoint(remoteAddr, SDP.IGNORE_RTP_PORT_NUMBER);
            }
            else
            {
                rtpEndPoint = new IPEndPoint(remoteAddr, announcement.Port);
            }
        }

        return rtpEndPoint;
    }

    private void SetGlobalDestination(IPEndPoint rtpEndPoint, IPEndPoint rtcpEndPoint)
    {
        _videoStream.SetDestination(rtpEndPoint, rtcpEndPoint);
    }

    private void SetGlobalSecurityContext(
        DtlsSrtpTransport rtpTransport,
        ProtectRtpPacket protectRtcp,
        ProtectRtpPacket unprotectRtcp)
    {
        _videoStream.SetSecurityContext(rtpTransport, protectRtcp, unprotectRtcp);
    }

    private void InitIPEndPointAndSecurityContext(MediaStream mediaStream)
    {
        // Get primary AudioStream
        if (mediaStream != null)
        {
            var secureContext = _videoStream.SecurityContext;
            if (secureContext != null)
            {
                mediaStream.SetSecurityContext(secureContext.RtpTransport, secureContext.ProtectRtcpPacket,
                    secureContext.UnprotectRtcpPacket);
            }

            mediaStream.SetDestination(_videoStream.DestinationEndPoint, _videoStream.ControlDestinationEndPoint);
        }
    }
    
    /// <summary>
    ///     Starts the RTCP session(s) that monitor this RTP session.
    /// </summary>
    private void Start()
    {
        if (!IsStarted)
        {
            IsStarted = true;
            if (_videoStream.HasVideo &&
                _videoStream.RtcpSession != null &&
                _videoStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
            {
                _videoStream.RtcpSession.Start();
            }
        }
    }

    public async Task SendVideoAsync(RtpPacket packet)
    {
        await _videoStream.SendRtpRawFromPacketAsync(packet);
    }

    private void OnReceive(int localPort, IPEndPoint remoteEndPoint, byte[] buffer)
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
            if (!IsSecureContextReady())
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
            if (_videoStream.LocalTrack == null)
            {
                OnRtcpBye?.Invoke(rtcpPkt.Bye.Reason);
            }
        }
        else if (!IsClosed)
        {
            if (mediaStream?.RtcpSession != null)
            {
                mediaStream.RtcpSession.ReportReceived();
                mediaStream.RaiseOnReceiveReportByIndex(remoteEndPoint, rtcpPkt);
            }
            else if (rtcpPkt.ReceiverReport?.Ssrc == RTCP_RR_NO_STREAM_SSRC)
            {
                // Ignore for the time being. Not sure what use an empty RTCP Receiver Report can provide.
            }
            else if (_videoStream.RtcpSession?.PacketsReceivedCount > 0)
            {
                // Only give this warning if we've received at least one RTP packet.
                //logger.LogWarning("Could not match an RTCP packet against any SSRC's in the session.");
                //logger.LogTrace(rtcpPkt.GetDebugSummary());
            }
        }
    }

    private MediaStream GetMediaStream(uint ssrc)
    {
        if (HasVideo)
        {
            return _videoStream;
        }

        if (_videoStream.LocalTrack?.IsSsrcMatch(ssrc) == true)
        {
            return _videoStream;
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
        if (found && 1 >= index)
        {
            return _videoStream;
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