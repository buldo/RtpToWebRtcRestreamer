//-----------------------------------------------------------------------------
// Filename: RTPSession.cs
//
// Description: Represents an RTP session constituted of a single media stream. The session
// does not control the sockets as they may be shared by multiple sessions.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 25 Aug 2019	Aaron Clauson	    Created, Montreux, Switzerland.
// 12 Nov 2019  Aaron Clauson       Added send event method.
// 07 Dec 2019  Aaron Clauson       Big refactor. Brought in a lot of functions previously
//                                  in the RTPChannel class.
// 26 Jul 2021  Kurt Kießling       Added secure media negotiation.
// 05 Apr 2022  Christophe Irles    Simplify file using MediaSteam, AudioStream and Video Stream
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

/// <summary>
/// The RTPSession class is the primary point for interacting with the Real-Time
/// Protocol. It manages all the resources required for setting up and then sending
/// and receiving RTP packets. This class IS designed to be inherited by child
/// classes and for child classes to add audio and video processing logic.
/// </summary>
/// <remarks>
/// The setting up of an RTP stream involved the exchange of Session Descriptions
/// (SDP) with the remote party. This class has adopted the mechanism used by WebRTC.
/// The steps are:
/// 1. If acting as the initiator:
///   a. Create offer,
///   b. Send offer to remote party and get their answer (external to this class, requires signalling),
///   c. Set remote description,
///   d. Optionally perform any additional set up, such as negotiating SRTP keying material,
///   e. Call Start to commence RTCP reporting.
/// 2. If acting as the recipient:
///   a. Receive offer,
///   b. Set remote description. This step MUST be done before an SDP answer can be generated.
///      This step can also result in an error condition if the codecs/formats offered aren't supported,
///   c. Create answer,
///   d. Send answer to remote party (external to this class, requires signalling),
///   e. Optionally perform any additional set up, such as negotiating SRTP keying material,
///   f. Call Start to commence RTCP reporting.
/// </remarks>
internal abstract class RTPSession : IDisposable
{
    /// <summary>
    /// From libsrtp: SRTP_MAX_TRAILER_LEN is the maximum length of the SRTP trailer
    /// (authentication tag and MKI) supported by libSRTP.This value is
    /// the maximum number of octets that will be added to an RTP packet by
    /// srtp_protect().
    ///
    /// srtp_protect():
    /// @warning This function assumes that it can write SRTP_MAX_TRAILER_LEN
    /// into the location in memory immediately following the RTP packet.
    /// Callers MUST ensure that this much writeable memory is available in
    /// the buffer that holds the RTP packet.
    ///
    /// srtp_protect_rtcp():
    /// @warning This function assumes that it can write SRTP_MAX_TRAILER_LEN+4
    /// to the location in memory immediately following the RTCP packet.
    /// Callers MUST ensure that this much writeable memory is available in
    /// the buffer that holds the RTCP packet.
    /// </summary>
    public const int SRTP_MAX_PREFIX_LENGTH = 148;
    protected internal const int DEFAULT_AUDIO_CLOCK_RATE = 8000;
    public const int DEFAULT_DTMF_EVENT_PAYLOAD_ID = 101;
    public const int DTMF_EVENT_PAYLOAD_ID = 101;

    /// <summary>
    /// When there are no RTP packets being sent for an audio or video stream webrtc.lib
    /// still sends RTCP Receiver Reports with this hard coded SSRC. No doubt it's defined
    /// in an RFC somewhere but I wasn't able to find it from a quick search.
    /// </summary>
    private const uint RTCP_RR_NOSTREAM_SSRC = 4195875351U;

    protected static readonly ILogger Logger = Log.Logger;

    protected readonly RtpSessionConfig RtpSessionConfig;

    internal int MRtpChannelsCount;            // Need to know the number of RTP Channels

    // The stream used for the underlying RTP session to create a single RTP channel that will
    // be used to multiplex all required media streams. (see addSingleTrack())
    private MediaStream _mPrimaryStream;

    protected RTPChannel MultiplexRtpChannel;

    private readonly List<List<SDPSsrcAttribute>> _audioRemoteSdpSsrcAttributes = new List<List<SDPSsrcAttribute>>();
    private readonly List<List<SDPSsrcAttribute>> _videoRemoteSdpSsrcAttributes = new List<List<SDPSsrcAttribute>>();

    /// <summary>
    /// The primary stream for this session - can be an AudioStream or a VideoStream
    /// </summary>
    protected MediaStream PrimaryStream
    {
        get
        {
            return _mPrimaryStream;
        }
    }

    /// <summary>
    /// The primary Audio Stream for this session
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
    /// The primary Video Stream for this session
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
    /// List of all Audio Streams for this session
    /// </summary>
    private List<AudioStream> AudioStreamList { get; } = new List<AudioStream>();

    /// <summary>
    /// List of all Video Streams for this session
    /// </summary>
    private List<VideoStream> VideoStreamList { get; } = new List<VideoStream>();

    /// <summary>
    /// The SDP offered by the remote call party for this session.
    /// </summary>
    protected SDP.SDP RemoteDescription { get; private set; }

    /// <summary>
    /// If this session is using a secure context this flag MUST be set to indicate
    /// the security delegate (SrtpProtect, SrtpUnprotect etc) have been set.
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
    /// Indicates whether the session has been closed. Once a session is closed it cannot
    /// be restarted.
    /// </summary>
    protected bool IsClosed { get; private set; }

    /// <summary>
    /// Indicates whether the session has been started. Starting a session tells the RTP
    /// socket to start receiving,
    /// </summary>
    private bool IsStarted { get; set; }

    /// <summary>
    /// Indicates whether this session is using audio.
    /// </summary>
    private bool HasAudio
    {
        get
        {
            return AudioStream?.HasAudio == true;
        }
    }

    /// <summary>
    /// Indicates whether this session is using video.
    /// </summary>
    private bool HasVideo
    {
        get
        {
            return VideoStream?.HasVideo == true;
        }
    }

    /// <summary>
    /// Gets fired when the RTP session and underlying channel are closed.
    /// </summary>
    public event Action<string> OnRtpClosed;

    /// <summary>
    /// Gets fired when an RTCP BYE packet is received from the remote party.
    /// The string parameter contains the BYE reason. Normally a BYE
    /// report means the RTP session is finished. But... cases have been observed where
    /// an RTCP BYE is received when a remote party is put on hold and then the session
    /// resumes when take off hold. It's up to the application to decide what action to
    /// take when n RTCP BYE is received.
    /// </summary>
    public event Action<string> OnRtcpBye;

    /// <summary>
    /// Gets fired when an RTCP report is received (the primary one). This event is for diagnostics only.
    /// </summary>
    public event Action<IPEndPoint, SDPMediaTypesEnum, RtcpCompoundPacket> OnReceiveReport;

    /// <summary>
    /// Creates a new RTP session. The synchronisation source and sequence number are initialised to
    /// pseudo random values.
    /// </summary>
    /// <param name="isRtcpMultiplexed">If true RTCP reports will be multiplexed with RTP on a single channel.
    /// If false (standard mode) then a separate socket is used to send and receive RTCP reports.</param>
    /// <param name="isSecure">If true indicated this session is using SRTP to encrypt and authorise
    /// RTP and RTCP packets. No communications or reporting will commence until the
    /// is explicitly set as complete.</param>
    /// <param name="isMediaMultiplexed">If true only a single RTP socket will be used for both audio
    /// and video (standard case for WebRTC). If false two separate RTP sockets will be used for
    /// audio and video (standard case for VoIP).</param>
    /// <param name="bindAddress">Optional. If specified this address will be used as the bind address for any RTP
    /// and control sockets created. Generally this address does not need to be set. The default behaviour
    /// is to bind to [::] or 0.0.0.0,d depending on system support, which minimises network routing
    /// causing connection issues.</param>
    /// <param name="bindPort">Optional. If specified a single attempt will be made to bind the RTP socket
    /// on this port. It's recommended to leave this parameter as the default of 0 to let the Operating
    /// System select the port number.</param>
    protected RTPSession(bool isMediaMultiplexed, bool isRtcpMultiplexed, bool isSecure, int bindPort = 0)
        : this(new RtpSessionConfig
        {
            IsMediaMultiplexed = isMediaMultiplexed,
            IsRtcpMultiplexed = isRtcpMultiplexed,
            RtpSecureMediaOption = isSecure ? RtpSecureMediaOptionEnum.DtlsSrtp : RtpSecureMediaOptionEnum.None,
            BindAddress = null,
            BindPort = bindPort
        })
    {
    }

    /// <summary>
    /// Creates a new RTP session. The synchronisation source and sequence number are initialised to
    /// pseudo random values.
    /// </summary>
    /// <param name="config">Contains required settings.</param>
    private RTPSession(RtpSessionConfig config)
    {
        RtpSessionConfig = config;
    }


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
        if (mediaStream.RtcpSession != null)
        {
            mediaStream.OnReceiveReportByIndex -= RaisedOnOnReceiveReport;
            mediaStream.RtcpSession.Close(reason);
            mediaStream.RtcpSession = null;
        }
    }

    private void RaisedOnOnReceiveReport(int index, IPEndPoint ipEndPoint, SDPMediaTypesEnum media, RtcpCompoundPacket report)
    {
        if (index == 0)
        {
            OnReceiveReport?.Invoke(ipEndPoint, media, report);
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
    /// Sets the remote SDP description for this session.
    /// </summary>
    /// <param name="sdpType">Whether the remote SDP is an offer or answer.</param>
    /// <param name="sessionDescription">The SDP that will be set as the remote description.</param>
    /// <returns>If successful an OK enum result. If not an enum result indicating the failure cause.</returns>
    protected virtual SetDescriptionResultEnum SetRemoteDescription(SdpType sdpType, SDP.SDP sessionDescription)
    {
        if (sessionDescription == null)
        {
            throw new ArgumentNullException(nameof(sessionDescription), "The session description cannot be null for SetRemoteDescription.");
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
                if (remoteMediaType == SDPMediaTypesEnum.audio && ((AudioStream == null) || (AudioStream.LocalTrack == null)))
                {
                    return SetDescriptionResultEnum.NoMatchingMediaType;
                }

                if (remoteMediaType == SDPMediaTypesEnum.video && ((VideoStream == null) || (VideoStream.LocalTrack == null)))
                {
                    return SetDescriptionResultEnum.NoMatchingMediaType;
                }
            }

            // Pre-flight checks have passed. Move onto matching up the local and remote media streams.
            IPAddress connectionAddress = null;
            if (sessionDescription.Connection != null && !string.IsNullOrEmpty(sessionDescription.Connection.ConnectionAddress))
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
                    remoteRtcpEp = (RtpSessionConfig.IsRtcpMultiplexed)
                        ? remoteRtpEp
                        : new IPEndPoint(remoteRtpEp.Address, remoteRtpEp.Port + 1);
                }

                currentMediaStream.DestinationEndPoint =
                    (remoteRtpEp != null && remoteRtpEp.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER)
                        ? remoteRtpEp
                        : currentMediaStream.DestinationEndPoint;
                currentMediaStream.ControlDestinationEndPoint =
                    (remoteRtcpEp != null && remoteRtcpEp.Port != SDP.SDP.IGNORE_RTP_PORT_NUMBER)
                        ? remoteRtcpEp
                        : currentMediaStream.ControlDestinationEndPoint;

                if (currentMediaStream.MediaType == SDPMediaTypesEnum.audio)
                {
                    if (capabilities?.Where(x => x.Name().ToLower() != SDP.SDP.TELEPHONE_EVENT_ATTRIBUTE).Count() == 0)
                    {
                        return SetDescriptionResultEnum.AudioIncompatible;
                    }
                }
                else if (capabilities?.Count == 0 || (currentMediaStream.LocalTrack == null && currentMediaStream.LocalTrack != null && currentMediaStream.LocalTrack.Capabilities?.Count == 0))
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
    /// Gets the RTP end point for an SDP media announcement from the remote peer.
    /// </summary>
    /// <param name="announcement">The media announcement to get the connection address for.</param>
    /// <param name="connectionAddress">The remote SDP session level connection address. Will be null if not available.</param>
    /// <returns>An IP end point for an SDP media announcement from the remote peer.</returns>
    private IPEndPoint GetAnnouncementRTPDestination(SDPMediaAnnouncement announcement, IPAddress connectionAddress)
    {
        var kind = announcement.Media;
        IPEndPoint rtpEndPoint = null;

        var remoteAddr = (announcement.Connection != null) ? IPAddress.Parse(announcement.Connection.ConnectionAddress) : connectionAddress;

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
    /// Used for child classes that require a single RTP channel for all RTP (audio and video)
    /// and RTCP communications.
    /// </summary>
    protected void AddSingleTrack(bool videoAsPrimary)
    {
        if (videoAsPrimary)
        {
            _mPrimaryStream = GetNextVideoStreamByLocalTrack();
        }
        else
        {
            _mPrimaryStream = GetNextAudioStreamByLocalTrack();
        }

        InitMediaStream(_mPrimaryStream);
    }

    private void InitMediaStream(MediaStream currentMediaStream)
    {
        var rtpChannel = CreateRtpChannel();
        currentMediaStream.RTPChannel = rtpChannel;
        CreateRtcpSession(currentMediaStream);
    }

#nullable enable
    /// <summary>
    /// Adds a media track to this session. A media track represents an audio or video
    /// stream and can be a local (which means we're sending) or remote (which means
    /// we're receiving).
    /// </summary>
    /// <param name="track">The media track to add to the session.</param>
    public void AddTrack(MediaStreamTrack track)
    {
        AddLocalTrack(track);
    }
#nullable restore
    /// <summary>
    /// Adds a local media stream to this session. Local media tracks should be added by the
    /// application to control what session description offers and answers can be made as
    /// well as being used to match up with remote tracks.
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

    protected void SetGlobalSecurityContext(DtlsSrtpTransport rtpTransport, ProtectRtpPacket protectRtcp, ProtectRtpPacket unprotectRtcp)
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
        if ((_mPrimaryStream != null) && (mediaStream != null))
        {
            var secureContext = _mPrimaryStream.SecurityContext;
            if (secureContext != null)
            {
                mediaStream.SetSecurityContext(secureContext.RtpTransport, secureContext.ProtectRtcpPacket, secureContext.UnprotectRtcpPacket);
            }
            mediaStream.SetDestination(_mPrimaryStream.DestinationEndPoint, _mPrimaryStream.ControlDestinationEndPoint);
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
    /// Adjust the stream status of the local media tracks based on the remote tracks.
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
                if (IPAddress.Any.Equals(remoteRTPEndPoint.Address) || IPAddress.IPv6Any.Equals(remoteRTPEndPoint.Address))
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
    /// Creates a new RTP channel (which manages the UDP socket sending and receiving RTP
    /// packets) for use with this session.
    /// </summary>
    /// <param name="mediaType">The type of media the RTP channel is for. Must be audio or video.</param>
    /// <returns>A new RTPChannel instance.</returns>
    protected abstract RTPChannel CreateRtpChannel();

    /// <summary>
    /// Gets the media streams available in this session. Will only be audio, video or both.
    /// media streams represent an audio or video source that we are sending to the remote party.
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
    /// Starts the RTCP session(s) that monitor this RTP session.
    /// </summary>
    protected Task Start()
    {
        if (!IsStarted)
        {
            IsStarted = true;


            foreach (var audioStream in AudioStreamList)
            {
                if (audioStream.HasAudio && audioStream.RtcpSession != null && audioStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
                {
                    // The local audio track may have been disabled if there were no matching capabilities with
                    // the remote party.
                    audioStream.RtcpSession.Start();
                }
            }

            foreach (var videoStream in VideoStreamList)
            {
                if (videoStream.HasVideo && videoStream.RtcpSession != null && videoStream.LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive)
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

    /// <summary>
    /// Close the session and RTP channel.
    /// </summary>
    public virtual void Close(string reason)
    {
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
        //logger.LogDebug($"RTCP packet received from {remoteEndPoint} {buffer.HexStr()}");

        #region RTCP packet.

        // Get the SSRC in order to be able to figure out which media type
        // This will let us choose the apropriate unprotect methods
        uint ssrc;
        if (BitConverter.IsLittleEndian)
        {
            ssrc = NetConvert.DoReverseEndian(BitConverter.ToUInt32(buffer, 4));
        }
        else
        {
            ssrc = BitConverter.ToUInt32(buffer, 4);
        }

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
            if (_mPrimaryStream.LocalTrack == null)
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
                    if ((mediaStream.ControlDestinationEndPoint == null ||
                         !mediaStream.ControlDestinationEndPoint.Address.Equals(remoteEndPoint.Address) ||
                         mediaStream.ControlDestinationEndPoint.Port != remoteEndPoint.Port))
                    {
                        Logger.LogDebug($"{mediaStream.MediaType} control end point switched from {mediaStream.ControlDestinationEndPoint} to {remoteEndPoint}.");
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
            else if (AudioStream?.RtcpSession?.PacketsReceivedCount > 0 || VideoStream?.RtcpSession?.PacketsReceivedCount > 0)
            {
                // Only give this warning if we've received at least one RTP packet.
                //logger.LogWarning("Could not match an RTCP packet against any SSRC's in the session.");
                //logger.LogTrace(rtcpPkt.GetDebugSummary());
            }
        }

        #endregion
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
        if (found && (AudioStreamList.Count > index))
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
        if (found && (VideoStreamList.Count > index))
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
    /// Attempts to get MediaStream that matches a received RTCP report.
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
    /// Event handler for the RTP channel closure.
    /// </summary>
    private void OnRTPChannelClosed(string reason)
    {
        Close(reason);
    }

    /// <summary>
    /// Close the session if the instance is out of scope.
    /// </summary>
    public virtual void Dispose()
    {
        Close("disposed");
    }
}