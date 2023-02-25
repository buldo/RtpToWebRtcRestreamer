using System.Net;
using Bld.RtpToWebRtcRestreamer.Common;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;

internal sealed class VideoStream
{
    private readonly ILogger _logger;

    /// <summary>
    /// Indicates the maximum frame size that can be reconstructed from RTP packets during the depacketisation
    /// process.
    /// </summary>
    private readonly int _maxReconstructedVideoFrameSize = 1048576;
    private RtpVideoFramer? _rtpVideoFramer;

    public VideoStream(
        int index,
        ILogger logger)
    {
        _index = index;
        _logger = logger;
    }

    /// <summary>
    /// Gets fired when a full video frame is reconstructed from one or more RTP packets
    /// received from the remote party.
    /// </summary>
    /// <remarks>
    ///  - Received from end point,
    ///  - The frame timestamp,
    ///  - The encoded video frame payload.
    ///  - The video format of the encoded frame.
    /// </remarks>
    public event Action<int, IPEndPoint, uint, byte[]>? OnVideoFrameReceivedByIndex;

    private void ProcessVideoRtpFrame(IPEndPoint endpoint, RTPPacket packet, VideoCodecsEnum codec)
    {
        if (OnVideoFrameReceivedByIndex == null)
        {
            return;
        }

        if (_rtpVideoFramer != null)
        {
            var frame = _rtpVideoFramer.GotRtpPacket(packet);
            if (frame != null)
            {
                OnVideoFrameReceivedByIndex?.Invoke(_index, endpoint, packet.Header.Timestamp, frame);
            }
        }
        else
        {
            if (codec == VideoCodecsEnum.VP8 ||
                codec == VideoCodecsEnum.H264)
            {
                _logger.LogDebug("Video depacketisation codec set to {Codec} for SSRC {SyncSource}.", codec, packet.Header.SyncSource);

                _rtpVideoFramer = new RtpVideoFramer(codec, _maxReconstructedVideoFrameSize);

                var frame = _rtpVideoFramer.GotRtpPacket(packet);
                if (frame != null)
                {
                    OnVideoFrameReceivedByIndex?.Invoke(_index, endpoint, packet.Header.Timestamp, frame);
                }
            }
            else
            {
                _logger.LogWarning("Video depacketisation logic for codec {codec} has not been implemented, PR's welcome!", codec);
            }
        }
    }

    private readonly int _index = -1;

    /// <summary>
    /// Gets fired when an RTP packet is received from a remote party.
    /// Parameters are:
    ///  - Remote endpoint packet was received from,
    ///  - The media type the packet contains, will be audio or video,
    ///  - The full RTP packet.
    /// </summary>
    public event Action<int, IPEndPoint, RTPPacket> OnRtpPacketReceivedByIndex;

    private bool AcceptRtpFromAny => true;

    /// <summary>
    /// The remote video track. Will be null if the remote party is not sending this media
    /// </summary>
    private MediaStreamTrack? RemoteTrack { get; set; }

    /// <summary>
    /// The remote RTP end point this stream is sending media to.
    /// </summary>
    private IPEndPoint DestinationEndPoint { get; set; }

    public void OnReceiveRTPPacket(RTPPacket rtpPacket, IPEndPoint remoteEndPoint)
    {
        // Set the remote track SSRC so that RTCP reports can match the media type.
        if (RemoteTrack != null && RemoteTrack.Ssrc == 0 && DestinationEndPoint != null)
        {
            var isValidSource = AdjustRemoteEndPoint(rtpPacket.Header.SyncSource, remoteEndPoint);

            if (isValidSource)
            {
                _logger.LogDebug($"Set remote track (index={_index}) SSRC to {rtpPacket.Header.SyncSource}.");
                RemoteTrack.Ssrc = rtpPacket.Header.SyncSource;
            }
        }

        if (RemoteTrack != null)
        {
            LogIfWrongSeqNumber($"", rtpPacket.Header, RemoteTrack);
        }

        var codec = GetFormatForPayloadId(rtpPacket.Header.PayloadType);
        if (codec != null)
        {
            ProcessVideoRtpFrame(remoteEndPoint, rtpPacket, codec.Value);
            RaiseOnRtpPacketReceivedByIndex(remoteEndPoint, rtpPacket);
        }
    }

    private VideoCodecsEnum? GetFormatForPayloadId(int hdrPayloadType)
    {
        if (hdrPayloadType == 97 || hdrPayloadType == 96)
        {
            return VideoCodecsEnum.H264;
        }

        return null;
    }

    private void RaiseOnRtpPacketReceivedByIndex(IPEndPoint ipEndPoint, RTPPacket rtpPacket)
    {
        OnRtpPacketReceivedByIndex?.Invoke(_index, ipEndPoint, rtpPacket);
    }

    private void LogIfWrongSeqNumber(string trackType, SIPSorcery.Net.RTP.RTPHeader header, MediaStreamTrack track)
    {
        if (track.LastRemoteSeqNum != 0 &&
            header.SequenceNumber != (track.LastRemoteSeqNum + 1) &&
            !(header.SequenceNumber == 0 && track.LastRemoteSeqNum == ushort.MaxValue))
        {
            _logger.LogWarning($"{trackType} stream sequence number jumped from {track.LastRemoteSeqNum} to {header.SequenceNumber}.");
        }
    }

    /// <summary>
    /// Adjusts the expected remote end point for a particular media type.
    /// </summary>
    /// <param name="mediaType">The media type of the RTP packet received.</param>
    /// <param name="ssrc">The SSRC from the RTP packet header.</param>
    /// <param name="receivedOnEndPoint">The actual remote end point that the RTP packet came from.</param>
    /// <returns>True if remote end point for this media type was the expected one or it was adjusted. False if
    /// the remote end point was deemed to be invalid for this media type.</returns>
    private bool AdjustRemoteEndPoint(uint ssrc, IPEndPoint receivedOnEndPoint)
    {
        var isValidSource = false;
        var expectedEndPoint = DestinationEndPoint;

        if (expectedEndPoint.Address.Equals(receivedOnEndPoint.Address) && expectedEndPoint.Port == receivedOnEndPoint.Port)
        {
            // Exact match on actual and expected destination.
            isValidSource = true;
        }
        else if (AcceptRtpFromAny || (expectedEndPoint.Address.IsPrivate() && !receivedOnEndPoint.Address.IsPrivate())
                //|| (IPAddress.Loopback.Equals(receivedOnEndPoint.Address) || IPAddress.IPv6Loopback.Equals(receivedOnEndPoint.Address
                )
        {
            // The end point doesn't match BUT we were supplied a private address in the SDP and the remote source is a public address
            // so high probability there's a NAT on the network path. Switch to the remote end point (note this can only happen once
            // and only if the SSRV is 0, i.e. this is the first RTP packet.
            // If the remote end point is a loopback address then it's likely that this is a test/development
            // scenario and the source can be trusted.
            // AC 12 Jul 2020: Commented out the expression that allows the end point to be change just because it's a loopback address.
            // A breaking case is doing an attended transfer test where two different agents are using loopback addresses.
            // The expression allows an older session to override the destination set by a newer remote SDP.
            // AC 18 Aug 2020: Despite the carefully crafted rules below and https://github.com/sipsorcery/sipsorcery/issues/197
            // there are still cases that were a problem in one scenario but acceptable in another. To accommodate a new property
            // was added to allow the application to decide whether the RTP end point switches should be liberal or not.
            _logger.LogDebug($" end point switched for RTP ssrc {ssrc} from {expectedEndPoint} to {receivedOnEndPoint}.");

            DestinationEndPoint = receivedOnEndPoint;

            isValidSource = true;
        }
        else
        {
            _logger.LogWarning($"RTP packet with SSRC {ssrc} received from unrecognised end point {receivedOnEndPoint}.");
        }

        return isValidSource;
    }
}