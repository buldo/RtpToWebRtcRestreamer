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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.Sys;

namespace SIPSorcery.net.RTP
{
    public class MediaStream
    {
        protected internal class PendingPackages
        {
            public RTPHeader hdr;
            public int localPort;
            public IPEndPoint remoteEndPoint;
            public byte[] buffer;
            public VideoStream videoStream;

            public PendingPackages(RTPHeader hdr, int localPort, IPEndPoint remoteEndPoint, byte[] buffer, VideoStream videoStream)
            {
                this.hdr = hdr;
                this.localPort = localPort;
                this.remoteEndPoint = remoteEndPoint;
                this.buffer = buffer;
                this.videoStream = videoStream;
            }
        }

        protected object _pendingPackagesLock = new object();
        protected List<PendingPackages> _pendingPackagesBuffer = new List<PendingPackages>();

        private static ILogger logger = Log.Logger;

        private RtpSessionConfig RtpSessionConfig;

        protected SecureContext SecureContext;
        
        MediaStreamTrack m_localTrack;

        protected RTPChannel rtpChannel;

        protected bool _isClosed;

        public int Index = -1;

        #region EVENTS

        /// <summary>
        /// Fires when the connection for a media type is classified as timed out due to not
        /// receiving any RTP or RTCP packets within the given period.
        /// </summary>
        public event Action<int, SDPMediaTypesEnum> OnTimeoutByIndex;
        
        /// <summary>
        /// Gets fired when an RTP packet is received from a remote party.
        /// Parameters are:
        ///  - Remote endpoint packet was received from,
        ///  - The media type the packet contains, will be audio or video,
        ///  - The full RTP packet.
        /// </summary>
        public event Action<int, IPEndPoint, SDPMediaTypesEnum, RTPPacket> OnRtpPacketReceivedByIndex;

        /// <summary>
        /// Gets fired when an RTP event is detected on the remote call party's RTP stream.
        /// </summary>
        public event Action<int, IPEndPoint, RTPEvent, RTPHeader> OnRtpEventByIndex;

        /// <summary>
        /// Gets fired when an RTCP report is received. This event is for diagnostics only.
        /// </summary>
        public event Action<int, IPEndPoint, SDPMediaTypesEnum, RTCPCompoundPacket> OnReceiveReportByIndex;

        public event Action<bool> OnIsClosedStateChanged;

        #endregion EVENTS

        #region PROPERTIES

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

                OnIsClosedStateChanged?.Invoke(_isClosed);
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
        public SDPMediaTypesEnum MediaType { get; set; }

        /// <summary>
        /// The local track. Will be null if we are not sending this media.
        /// </summary>
        public MediaStreamTrack LocalTrack
        {
            get
            {
                return m_localTrack;
            }
            set
            {
                m_localTrack = value;
                if (m_localTrack != null)
                {
                    // Need to create a sending SSRC and set it on the RTCP session. 
                    if (RtcpSession != null)
                    {
                        RtcpSession.Ssrc = m_localTrack.Ssrc;
                    }

                    if (MediaType == SDPMediaTypesEnum.audio)
                    {
                        if (m_localTrack.Capabilities != null && !m_localTrack.NoDtmfSupport &&
                            !m_localTrack.Capabilities.Any(x => x.ID == RTPSession.DTMF_EVENT_PAYLOAD_ID))
                        {
                            SDPAudioVideoMediaFormat rtpEventFormat = new SDPAudioVideoMediaFormat(
                                SDPMediaTypesEnum.audio,
                                RTPSession.DTMF_EVENT_PAYLOAD_ID,
                                SDP.TELEPHONE_EVENT_ATTRIBUTE,
                                RTPSession.DEFAULT_AUDIO_CLOCK_RATE,
                                SDPAudioVideoMediaFormat.DEFAULT_AUDIO_CHANNEL_COUNT,
                                "0-16");
                            m_localTrack.Capabilities.Add(rtpEventFormat);
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

        #endregion PROPERTIES

        #region SECURITY CONTEXT

        public void SetSecurityContext(ProtectRtpPacket protectRtp, ProtectRtpPacket unprotectRtp, ProtectRtpPacket protectRtcp, ProtectRtpPacket unprotectRtcp)
        {
            if (SecureContext != null)
            {
                logger.LogTrace($"Tried adding new SecureContext for media type {MediaType}, but one already existed");
            }

            SecureContext = new SecureContext(protectRtp, unprotectRtp, protectRtcp, unprotectRtcp);

            DispatchPendingPackages();
        }

        public SecureContext GetSecurityContext()
        {
            return SecureContext;
        }

        public Boolean IsSecurityContextReady()
        {
            return (SecureContext != null);
        }

        private (bool, byte[]) UnprotectBuffer(byte[] buffer)
        {
            if (SecureContext != null)
            {
                int res = SecureContext.UnprotectRtpPacket(buffer, buffer.Length, out int outBufLen);

                if (res == 0)
                {
                    return (true, buffer.Take(outBufLen).ToArray());
                }

                logger.LogWarning($"SRTP unprotect failed for {MediaType}, result {res}.");
            }
            return (false, buffer);
        }

        public bool EnsureBufferUnprotected(byte[] buf, RTPHeader header, out RTPPacket packet)
        {
            if (RtpSessionConfig.IsSecure || RtpSessionConfig.UseSdpCryptoNegotiation)
            {
                var (succeeded, newBuffer) = UnprotectBuffer(buf);
                if (!succeeded)
                {
                    packet = null;
                    return false;
                }
                packet = new RTPPacket(newBuffer);
            }
            else
            {
                packet = new RTPPacket(buf);
            }
            packet.Header.ReceivedTime = header.ReceivedTime;
            return true;
        }

        #endregion SECURITY CONTEXT

        #region RTP CHANNEL

        public void AddRtpChannel(RTPChannel rtpChannel)
        {
            this.rtpChannel = rtpChannel;
        }

        public Boolean HasRtpChannel()
        {
            return rtpChannel != null;
        }

        public RTPChannel GetRTPChannel()
        {
            return rtpChannel;
        }

        #endregion RTP CHANNEL

        #region SEND PACKET

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

            if ((RtpSessionConfig.IsSecure || RtpSessionConfig.UseSdpCryptoNegotiation) && SecureContext?.ProtectRtpPacket == null)
            {
                logger.LogWarning("SendRtpPacket cannot be called on a secure session before calling SetSecurityContext.");
                return false;
            }

            return true;
        }

        protected void SendRtpRaw(byte[] data, uint timestamp, int markerBit, int payloadType, Boolean checkDone)
        {
            if (checkDone || CheckIfCanSendRtpRaw())
            {
                ProtectRtpPacket protectRtpPacket = SecureContext?.ProtectRtpPacket;
                int srtpProtectionLength = (protectRtpPacket != null) ? RTPSession.SRTP_MAX_PREFIX_LENGTH : 0;

                RTPPacket rtpPacket = new RTPPacket(data.Length + srtpProtectionLength);
                rtpPacket.Header.SyncSource = LocalTrack.Ssrc;
                rtpPacket.Header.SequenceNumber = LocalTrack.GetNextSeqNum();
                rtpPacket.Header.Timestamp = timestamp;
                rtpPacket.Header.MarkerBit = markerBit;
                rtpPacket.Header.PayloadType = payloadType;

                Buffer.BlockCopy(data, 0, rtpPacket.Payload, 0, data.Length);

                var rtpBuffer = rtpPacket.GetBytes();

                if (protectRtpPacket == null)
                {
                    rtpChannel.Send(RTPChannelSocketsEnum.RTP, DestinationEndPoint, rtpBuffer);
                }
                else
                {
                    int rtperr = protectRtpPacket(rtpBuffer, rtpBuffer.Length - srtpProtectionLength, out var outBufLen);
                    if (rtperr != 0)
                    {
                        logger.LogError("SendRTPPacket protection failed, result " + rtperr + ".");
                    }
                    else
                    {
                        rtpChannel.Send(RTPChannelSocketsEnum.RTP, DestinationEndPoint, rtpBuffer.Take(outBufLen).ToArray());
                    }
                }
                
                RtcpSession?.RecordRtpPacketSend(rtpPacket);
            }
        }

        #endregion SEND PACKET

        #region RECEIVE PACKET

        public void OnReceiveRTPPacket(RTPHeader hdr, int localPort, IPEndPoint remoteEndPoint, byte[] buffer, VideoStream videoStream = null)
        {
            RTPPacket rtpPacket;
            if (RemoteRtpEventPayloadID != 0 && hdr.PayloadType == RemoteRtpEventPayloadID)
            {
                if (!EnsureBufferUnprotected(buffer, hdr, out rtpPacket))
                {
                    // Cache pending packages to use it later to prevent missing frames
                    // when DTLS was not completed yet as a Server bt already completed as a client
                    AddPendingPackage(hdr, localPort, remoteEndPoint, buffer, videoStream);
                    return;
                }

                RaiseOnRtpEventByIndex(remoteEndPoint, new RTPEvent(rtpPacket.Payload), rtpPacket.Header);
                return;
            }
            

            // Note AC 24 Dec 2020: The problem with waiting until the remote description is set is that the remote peer often starts sending
            // RTP packets at the same time it signals its SDP offer or answer. Generally this is not a problem for audio but for video streams
            // the first RTP packet(s) are the key frame and if they are ignored the video stream will take additional time or manual 
            // intervention to synchronise.
            //if (RemoteDescription != null)
            //{

            // Don't hand RTP packets to the application until the remote description has been set. Without it
            // things like the common codec, DTMF support etc. are not known.

            //SDPMediaTypesEnum mediaType = (rtpMediaType.HasValue) ? rtpMediaType.Value : DEFAULT_MEDIA_TYPE;

            // For video RTP packets an attempt will be made to collate into frames. It's up to the application
            // whether it wants to subscribe to frames of RTP packets.

            rtpPacket = null;
            if (!EnsureBufferUnprotected(buffer, hdr, out rtpPacket))
            {
                return;
            }

            // When receiving an Payload from other peer, it will be related to our LocalDescription,
            // not to RemoteDescription (as proved by Azure WebRTC Implementation)
            var format = LocalTrack?.GetFormatForPayloadID(hdr.PayloadType);
            if ((rtpPacket != null) && (format != null))
            {
                videoStream?.ProcessVideoRtpFrame(remoteEndPoint, rtpPacket, format.Value);
                RaiseOnRtpPacketReceivedByIndex(remoteEndPoint, rtpPacket);
                
                RtcpSession?.RecordRtpPacketReceived(rtpPacket);
            }
        }

        #endregion RECEIVE PACKET

        #region TO RAISE EVENTS FROM INHERITED CLASS

        public void RaiseOnReceiveReportByIndex(IPEndPoint ipEndPoint, RTCPCompoundPacket rtcpPCompoundPacket)
        {
            OnReceiveReportByIndex?.Invoke(Index, ipEndPoint, MediaType, rtcpPCompoundPacket);
        }

        protected void RaiseOnRtpEventByIndex(IPEndPoint ipEndPoint, RTPEvent rtpEvent, RTPHeader rtpHeader)
        {
            OnRtpEventByIndex?.Invoke(Index, ipEndPoint, rtpEvent, rtpHeader);
        }

        protected void RaiseOnRtpPacketReceivedByIndex(IPEndPoint ipEndPoint, RTPPacket rtpPacket)
        {
            OnRtpPacketReceivedByIndex?.Invoke(Index, ipEndPoint, MediaType, rtpPacket);
        }

        private void RaiseOnTimeoutByIndex(SDPMediaTypesEnum mediaType)
        {
            OnTimeoutByIndex?.Invoke(Index, mediaType);
        }

        #endregion TO RAISE EVENTS FROM INHERITED CLASS

        #region PENDING PACKAGES LOGIC

        // Submit all previous cached packages to self
        protected void DispatchPendingPackages()
        {
            PendingPackages[] pendingPackagesArray = null;

            var isContextValid = SecureContext != null && !IsClosed;

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

            if (SecureContext == null && !IsClosed)
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

        #endregion

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
                RtcpSession.OnTimeout += RaiseOnTimeoutByIndex;
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
