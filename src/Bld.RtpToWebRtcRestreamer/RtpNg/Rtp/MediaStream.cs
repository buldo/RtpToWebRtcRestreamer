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

using System.Buffers;
using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;

internal abstract class MediaStream
{
    private static readonly ILogger Logger = Log.Logger;

    private readonly ArrayPool<byte> _sendBuffersPool = ArrayPool<byte>.Shared;
    private readonly ObjectPool<RtpPacket> _packetsPool =
        new DefaultObjectPool<RtpPacket>(new DefaultPooledObjectPolicy<RtpPacket>(), 5);
    private readonly RtpSessionConfig _rtpSessionConfig;

    private SecureContext _secureContext;
    private MediaStreamTrack _mLocalTrack;

    private readonly int _index;

    protected MediaStream(RtpSessionConfig config, int index)
    {
        _rtpSessionConfig = config;
        _index = index;
    }

    /// <summary>
    /// Gets fired when an RTCP report is received. This event is for diagnostics only.
    /// </summary>
    public event Action<int, IPEndPoint, SDPMediaTypesEnum, RtcpCompoundPacket> OnReceiveReportByIndex;

    /// <summary>
    /// Indicates whether the session has been closed. Once a session is closed it cannot
    /// be restarted.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// To type of this media
    /// </summary>
    public abstract SDPMediaTypesEnum MediaType { get; }

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
            }
        }
    }

    /// <summary>
    /// The reporting session for this media stream.
    /// </summary>
    public RtcpSession RtcpSession { get; set; }

    /// <summary>
    /// The remote RTP end point this stream is sending media to.
    /// </summary>
    public IPEndPoint DestinationEndPoint { get; set; }

    /// <summary>
    /// The remote RTP control end point this stream is sending to RTCP reports for the media stream to.
    /// </summary>
    public IPEndPoint ControlDestinationEndPoint { get; set; }

    public RTPChannel RTPChannel { get; set; }

    public SecureContext SecurityContext => _secureContext;

    public void SetSecurityContext(DtlsSrtpTransport rtpTransport, ProtectRtpPacket protectRtcp, ProtectRtpPacket unprotectRtcp)
    {
        if (_secureContext != null)
        {
            Logger.LogTrace($"Tried adding new SecureContext for media type {MediaType}, but one already existed");
        }

        _secureContext = new SecureContext(rtpTransport, protectRtcp, unprotectRtcp);
    }

    public bool IsSecurityContextReady()
    {
        return _secureContext != null;
    }

    public bool HasRtpChannel()
    {
        return RTPChannel != null;
    }

    public void RaiseOnReceiveReportByIndex(IPEndPoint ipEndPoint, RtcpCompoundPacket rtcpPCompoundPacket)
    {
        OnReceiveReportByIndex?.Invoke(_index, ipEndPoint, MediaType, rtcpPCompoundPacket);
    }

    /// <summary>
    /// Creates a new RTCP session for a media track belonging to this RTP session.
    /// </summary>
    /// <returns>A new RTCPSession object. The RTCPSession must have its Start method called
    /// in order to commence sending RTCP reports.</returns>
    public bool CreateRtcpSession()
    {
        if (RtcpSession == null)
        {
            RtcpSession = new RtcpSession(0);
            return true;
        }
        return false;
    }


    /// <summary>
    /// Sets the remote end points for a media type supported by this RTP session.
    /// </summary>
    /// <param name="rtpEndPoint">The remote end point for RTP packets corresponding to the media type.</param>
    /// <param name="rtcpEndPoint">The remote end point for RTCP packets corresponding to the media type.</param>
    public void SetDestination(IPEndPoint rtpEndPoint, IPEndPoint rtcpEndPoint)
    {
        DestinationEndPoint = rtpEndPoint;
        ControlDestinationEndPoint = rtcpEndPoint;
    }

    public async Task SendRtpRawFromPacketAsync(RtpPacket packet)
    {
        if (CheckIfCanSendRtpRaw())
        {
            var localBuffer = _sendBuffersPool.Rent(Constants.MAX_UDP_SIZE);
            packet.Header.WriteTo(localBuffer.AsSpan(0,packet.Header.Length));
            packet.Payload.CopyTo(localBuffer.AsSpan(packet.Header.Length));
            var rtpPacket = _packetsPool.Get();
            rtpPacket.ApplyBuffer(localBuffer, 0, localBuffer.Length);
            rtpPacket.Header.SyncSource = LocalTrack.Ssrc;
            rtpPacket.Header.SequenceNumber = LocalTrack.GetNextSeqNum();

            rtpPacket.ApplyHeaderChanges();

            var requestedLen = packet.Header.Length + packet.Payload.Length + RTPSession.SRTP_MAX_PREFIX_LENGTH;
            var rtperr = _secureContext.RtpTransport.ProtectRTP(
                rtpPacket.Header.SyncSource,
                localBuffer,
                requestedLen - RTPSession.SRTP_MAX_PREFIX_LENGTH,
                out var outBufLen);
            if (rtperr != 0)
            {
                Logger.LogError("SendRTPPacket protection failed, result " + rtperr + ".");
            }
            else
            {
                await RTPChannel.SendAsync(DestinationEndPoint, localBuffer.AsMemory(0, outBufLen));
            }

            RtcpSession?.RecordRtpPacketSend(rtpPacket);
            var released = rtpPacket.ReleaseBuffer();
            _sendBuffersPool.Return(released);
            _packetsPool.Return(rtpPacket);
        }
    }

    private bool CheckIfCanSendRtpRaw()
    {
        if (IsClosed)
        {
            Logger.LogWarning($"SendRtpRaw was called for an {MediaType} packet on an closed RTP session.");
            return false;
        }

        if (LocalTrack == null)
        {
            Logger.LogWarning($"SendRtpRaw was called for an {MediaType} packet on an RTP session without a local track.");
            return false;
        }

        if ((LocalTrack.StreamStatus == MediaStreamStatusEnum.RecvOnly) || (LocalTrack.StreamStatus == MediaStreamStatusEnum.Inactive))
        {
            Logger.LogWarning($"SendRtpRaw was called for an {MediaType} packet on an RTP session with a Stream Status set to {LocalTrack.StreamStatus}");
            return false;
        }

        if ((_rtpSessionConfig.IsSecure || _rtpSessionConfig.UseSdpCryptoNegotiation) && _secureContext?.RtpTransport == null)
        {
            Logger.LogWarning("SendRtpPacket cannot be called on a secure session before calling SetSecurityContext.");
            return false;
        }

        if (_secureContext == null)
        {
            return false;
        }

        return true;
    }
}