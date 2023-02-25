//-----------------------------------------------------------------------------
// Filename: MediaStream.cs
//
// Description: Define a Media Stream to centralize all related objects: local/remote tracks, rtcp session, ip end point
// The goal is to simplify RTPSession class
//
// Author(s):
// Christophe Irles
//
// History:
// 05 Apr 2022	Christophe Irles        Created (based on existing code from previous RTPSession class)
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using Bld.RtpToWebRtcRestreamer.Common;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTCP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    internal class MediaStream
    {
        private class PendingPackages
        {
            public readonly RTPHeader hdr;
            public readonly int localPort;
            public readonly IPEndPoint remoteEndPoint;
            public readonly byte[] buffer;
            public readonly VideoStream videoStream;

            public PendingPackages(RTPHeader hdr, int localPort, IPEndPoint remoteEndPoint, byte[] buffer, VideoStream videoStream)
            {
                this.hdr = hdr;
                this.localPort = localPort;
                this.remoteEndPoint = remoteEndPoint;
                this.buffer = buffer;
                this.videoStream = videoStream;
            }
        }

        private readonly object _pendingPackagesLock = new object();
        private readonly List<PendingPackages> _pendingPackagesBuffer = new();

        private static readonly ILogger logger = Log.Logger;

        private readonly RtpSessionConfig RtpSessionConfig;

        private SecureContext _secureContext;

        MediaStreamTrack _mLocalTrack;

        private RTPChannel _rtpChannel;

        private bool _isClosed;

        protected int Index = -1;

        /// <summary>
        /// Gets fired when an RTCP report is received. This event is for diagnostics only.
        /// </summary>
        public event Action<int, IPEndPoint, SDPMediaTypesEnum, RTCPCompoundPacket> OnReceiveReportByIndex;

        public Boolean AcceptRtpFromAny { get; set; }

        /// <summary>
        /// Indicates whether the session has been closed. Once a session is closed it cannot
        /// be restarted.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                return _isClosed;
            }
            set
            {
                if (_isClosed == value)
                {
                    return;
                }
                _isClosed = value;

                //Clear previous buffer
                ClearPendingPackages();
            }
        }

        /// <summary>
        /// In order to detect RTP events from the remote party this property needs to
        /// be set to the payload ID they are using.
        /// </summary>
        public int RemoteRtpEventPayloadID { get; set; } = RTPSession.DEFAULT_DTMF_EVENT_PAYLOAD_ID;

        /// <summary>
        /// To type of this media
        /// </summary>
        public SDPMediaTypesEnum MediaType { get; protected set; }

        /// <summary>
        /// The local track. Will be null if we are not sending this media.
        /// </summary>
        public MediaStreamTrack LocalTrack
        {
            get
            {
                return _mLocalTrack;
            }
            set
            {
                _mLocalTrack = value;
                if (_mLocalTrack != null)
                {
                    // Need to create a sending SSRC and set it on the RTCP session.
                    if (RtcpSession != null)
                    {
                        RtcpSession.Ssrc = _mLocalTrack.Ssrc;
                    }

                    if (MediaType == SDPMediaTypesEnum.audio)
                    {
                        if (_mLocalTrack.Capabilities != null && !_mLocalTrack.NoDtmfSupport &&
                            !_mLocalTrack.Capabilities.Any(x => x.ID == RTPSession.DTMF_EVENT_PAYLOAD_ID))
                        {
                            var rtpEventFormat = new SDPAudioVideoMediaFormat(
                                SDPMediaTypesEnum.audio,
                                RTPSession.DTMF_EVENT_PAYLOAD_ID,
                                SDP.SDP.TELEPHONE_EVENT_ATTRIBUTE,
                                RTPSession.DEFAULT_AUDIO_CLOCK_RATE,
                                SDPAudioVideoMediaFormat.DEFAULT_AUDIO_CHANNEL_COUNT,
                                "0-16");
                            _mLocalTrack.Capabilities.Add(rtpEventFormat);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The reporting session for this media stream.
        /// </summary>
        public RTCPSession RtcpSession { get; set; }

        /// <summary>
        /// The remote RTP end point this stream is sending media to.
        /// </summary>
        public IPEndPoint DestinationEndPoint { get; set; }

        /// <summary>
        /// The remote RTP control end point this stream is sending to RTCP reports for the media stream to.
        /// </summary>
        public IPEndPoint ControlDestinationEndPoint { get; set; }

        public void SetSecurityContext(ProtectRtpPacket protectRtp, ProtectRtpPacket unprotectRtp, ProtectRtpPacket protectRtcp, ProtectRtpPacket unprotectRtcp)
        {
            if (_secureContext != null)
            {
                logger.LogTrace($"Tried adding new SecureContext for media type {MediaType}, but one already existed");
            }

            _secureContext = new SecureContext(protectRtp, unprotectRtp, protectRtcp, unprotectRtcp);

            DispatchPendingPackages();
        }

        public SecureContext GetSecurityContext()
        {
            return _secureContext;
        }

        public Boolean IsSecurityContextReady()
        {
            return (_secureContext != null);
        }

        private (bool, byte[]) UnprotectBuffer(byte[] buffer)
        {
            if (_secureContext != null)
            {
                var res = _secureContext.UnprotectRtpPacket(buffer, buffer.Length, out var outBufLen);

                if (res == 0)
                {
                    return (true, buffer.Take(outBufLen).ToArray());
                }

                logger.LogWarning($"SRTP unprotect failed for {MediaType}, result {res}.");
            }
            return (false, buffer);
        }

        private bool EnsureBufferUnprotected(byte[] buf)
        {
            if (RtpSessionConfig.IsSecure || RtpSessionConfig.UseSdpCryptoNegotiation)
            {
                var (succeeded, _) = UnprotectBuffer(buf);
                if (!succeeded)
                {
                    return false;
                }
            }

            return true;
        }

        public void AddRtpChannel(RTPChannel rtpChannel)
        {
            _rtpChannel = rtpChannel;
        }

        public Boolean HasRtpChannel()
        {
            return _rtpChannel != null;
        }

        public RTPChannel GetRTPChannel()
        {
            return _rtpChannel;
        }

        protected Boolean CheckIfCanSendRtpRaw()
        {
            if (IsClosed)
            {
                logger.LogWarning($"SendRtpRaw was called for an {MediaType} packet on an closed RTP session.");
                return false;
            }

            if (LocalTrack == null)
            {
                logger.LogWarning($"SendRtpRaw was called for an {MediaType} packet on an RTP session without a local track.");
                return false;
            }

            if ((LocalTrack.StreamStatus == MediaStreamStatusEnum.RecvOnly) || (LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive))
            {
                logger.LogWarning($"SendRtpRaw was called for an {MediaType} packet on an RTP session with a Stream Status set to {LocalTrack.StreamStatus}");
                return false;
            }

            if ((RtpSessionConfig.IsSecure || RtpSessionConfig.UseSdpCryptoNegotiation) && _secureContext?.ProtectRtpPacket == null)
            {
                logger.LogWarning("SendRtpPacket cannot be called on a secure session before calling SetSecurityContext.");
                return false;
            }

            return true;
        }

        protected void SendRtpRawFromPacket(RTPPacket packet)
        {
            if (CheckIfCanSendRtpRaw()) {
                var protectRtpPacket = _secureContext?.ProtectRtpPacket;
                var srtpProtectionLength = (protectRtpPacket != null) ? RTPSession.SRTP_MAX_PREFIX_LENGTH : 0;

                var rtpPacket = new RTPPacket(packet.Payload.Length + srtpProtectionLength);
                rtpPacket.Header.SyncSource = LocalTrack.Ssrc;
                rtpPacket.Header.SequenceNumber = LocalTrack.GetNextSeqNum();
                rtpPacket.Header.Timestamp = packet.Header.Timestamp;
                rtpPacket.Header.MarkerBit = packet.Header.MarkerBit;
                rtpPacket.Header.PayloadType = packet.Header.PayloadType;

                Buffer.BlockCopy(packet.Payload, 0, rtpPacket.Payload, 0, packet.Payload.Length);

                var rtpBuffer = rtpPacket.GetBytes();

                if (protectRtpPacket == null) {
                    _rtpChannel.Send(RTPChannelSocketsEnum.RTP, DestinationEndPoint, rtpBuffer);
                }
                else {
                    var rtperr = protectRtpPacket(rtpBuffer, rtpBuffer.Length - srtpProtectionLength, out var outBufLen);
                    if (rtperr != 0) {
                        logger.LogError("SendRTPPacket protection failed, result " + rtperr + ".");
                    }
                    else {
                        _rtpChannel.Send(RTPChannelSocketsEnum.RTP, DestinationEndPoint, rtpBuffer.Take(outBufLen).ToArray());
                    }
                }

                RtcpSession?.RecordRtpPacketSend(rtpPacket);
            }
        }

        private void OnReceiveRTPPacket(RTPHeader hdr, int localPort, IPEndPoint remoteEndPoint, byte[] buffer, VideoStream videoStream = null)
        {
            if (RemoteRtpEventPayloadID != 0 && hdr.PayloadType == RemoteRtpEventPayloadID)
            {
                if (!EnsureBufferUnprotected(buffer))
                {
                    // Cache pending packages to use it later to prevent missing frames
                    // when DTLS was not completed yet as a Server bt already completed as a client
                    AddPendingPackage(hdr, localPort, remoteEndPoint, buffer, videoStream);
                }
            }
        }

        public void RaiseOnReceiveReportByIndex(IPEndPoint ipEndPoint, RTCPCompoundPacket rtcpPCompoundPacket)
        {
            OnReceiveReportByIndex?.Invoke(Index, ipEndPoint, MediaType, rtcpPCompoundPacket);
        }

        // Submit all previous cached packages to self
        private void DispatchPendingPackages()
        {
            PendingPackages[] pendingPackagesArray = null;

            var isContextValid = _secureContext != null && !IsClosed;

            lock (_pendingPackagesLock)
            {
                if (isContextValid)
                {
                    pendingPackagesArray = _pendingPackagesBuffer.ToArray();
                }
                _pendingPackagesBuffer.Clear();
            }
            if (isContextValid)
            {
                foreach (var pendingPackage in pendingPackagesArray)
                {
                    if (pendingPackage != null)
                    {
                        OnReceiveRTPPacket(pendingPackage.hdr, pendingPackage.localPort, pendingPackage.remoteEndPoint, pendingPackage.buffer, pendingPackage.videoStream);
                    }
                }
            }
        }

        // Clear previous buffer
        private void ClearPendingPackages()
        {
            lock (_pendingPackagesLock)
            {
                _pendingPackagesBuffer.Clear();
            }
        }

        // Cache pending packages to use it later to prevent missing frames
        // when DTLS was not completed yet as a Server but already completed as a client
        private void AddPendingPackage(RTPHeader hdr, int localPort, IPEndPoint remoteEndPoint, byte[] buffer,
            VideoStream videoStream = null)
        {
            const int MAX_PENDING_PACKAGES_BUFFER_SIZE = 32;

            if (_secureContext == null && !IsClosed)
            {
                lock (_pendingPackagesLock)
                {
                    //ensure buffer max size
                    while (_pendingPackagesBuffer.Count > 0 && _pendingPackagesBuffer.Count >= MAX_PENDING_PACKAGES_BUFFER_SIZE)
                    {
                        _pendingPackagesBuffer.RemoveAt(0);
                    }
                    _pendingPackagesBuffer.Add(new PendingPackages(hdr, localPort, remoteEndPoint, buffer, videoStream));
                }
            }
        }

        /// <summary>
        /// Creates a new RTCP session for a media track belonging to this RTP session.
        /// </summary>
        /// <param name="mediaType">The media type to create the RTP session for. Must be
        /// audio or video.</param>
        /// <returns>A new RTCPSession object. The RTCPSession must have its Start method called
        /// in order to commence sending RTCP reports.</returns>
        public bool CreateRtcpSession()
        {
            if (RtcpSession == null)
            {
                RtcpSession = new RTCPSession(MediaType, 0);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the remote end points for a media type supported by this RTP session.
        /// </summary>
        /// <param name="mediaType">The media type, must be audio or video, to set the remote end point for.</param>
        /// <param name="rtpEndPoint">The remote end point for RTP packets corresponding to the media type.</param>
        /// <param name="rtcpEndPoint">The remote end point for RTCP packets corresponding to the media type.</param>
        public void SetDestination(IPEndPoint rtpEndPoint, IPEndPoint rtcpEndPoint)
        {
            DestinationEndPoint = rtpEndPoint;
            ControlDestinationEndPoint = rtcpEndPoint;
        }

        /// <summary>
        /// Attempts to get the highest priority sending format for the remote call party.
        /// </summary>
        /// <returns>The first compatible media format found for the specified media type.</returns>
        protected SDPAudioVideoMediaFormat GetSendingFormat()
        {
            if (LocalTrack != null)
            {

                return LocalTrack.Capabilities.First();
            }

            throw new ApplicationException($"Cannot get the {MediaType} sending format, missing either local or remote {MediaType} track.");
        }

        protected MediaStream(RtpSessionConfig config, int index)
        {
            RtpSessionConfig = config;
            Index = index;
        }
    }
}
