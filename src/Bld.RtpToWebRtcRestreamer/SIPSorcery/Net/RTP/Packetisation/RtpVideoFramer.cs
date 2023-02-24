//-----------------------------------------------------------------------------
// Filename: RtpVideoFramer.cs
//
// Description: Video frames can be spread across multiple RTP packets. The
// purpose of this class is to put the RTP packets together to get back the
// encoded video frame.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 04 Sep 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP.Packetisation
{
    public class RtpVideoFramer
    {
        private static ILogger logger = Log.Logger;

        private VideoCodecsEnum _codec;
        private int _maxFrameSize;
        private byte[] _currVideoFrame;
        private int _currVideoFramePosn;
        private H264Depacketiser _h264Depacketiser;

        public RtpVideoFramer(VideoCodecsEnum codec, int maxFrameSize)
        {
            if (!(codec == VideoCodecsEnum.VP8 || codec == VideoCodecsEnum.H264))
            {
                throw new NotSupportedException("The RTP video framer currently only understands H264 and VP8 encoded frames.");
            }

            _codec = codec;
            _maxFrameSize = maxFrameSize;
            _currVideoFrame = new byte[maxFrameSize];
            
            if (_codec == VideoCodecsEnum.H264)
            {
                _h264Depacketiser = new H264Depacketiser();
            }
        }

        public byte[] GotRtpPacket(RTPPacket rtpPacket)
        {
            var payload = rtpPacket.Payload;

            var hdr = rtpPacket.Header;

            if (_codec == VideoCodecsEnum.VP8)
            {
                //logger.LogDebug($"rtp VP8 video, seqnum {hdr.SequenceNumber}, ts {hdr.Timestamp}, marker {hdr.MarkerBit}, payload {payload.Length}.");

                if (_currVideoFramePosn + payload.Length >= _maxFrameSize)
                {
                    // Something has gone very wrong. Clear the buffer.
                    _currVideoFramePosn = 0;
                }

                // New frames must have the VP8 Payload Descriptor Start bit set.
                // The tracking of the current video frame position is to deal with a VP8 frame being split across multiple RTP packets
                // as per https://tools.ietf.org/html/rfc7741#section-4.4.
                if (_currVideoFramePosn > 0 || (payload[0] & 0x10) > 0)
                {
                    RtpVP8Header vp8Header = RtpVP8Header.GetVP8Header(payload);

                    Buffer.BlockCopy(payload, vp8Header.Length, _currVideoFrame, _currVideoFramePosn, payload.Length - vp8Header.Length);
                    _currVideoFramePosn += payload.Length - vp8Header.Length;

                    if (rtpPacket.Header.MarkerBit > 0)
                    {
                        var frame = _currVideoFrame.Take(_currVideoFramePosn).ToArray();

                        _currVideoFramePosn = 0;

                        return frame;
                    }
                }
                else
                {
                    logger.LogWarning("Discarding RTP packet, VP8 header Start bit not set.");
                    //logger.LogWarning($"rtp video, seqnum {hdr.SequenceNumber}, ts {hdr.Timestamp}, marker {hdr.MarkerBit}, payload {payload.Length}.");
                }
            }
            else if (_codec == VideoCodecsEnum.H264)
            {
                //logger.LogDebug($"rtp H264 video, seqnum {hdr.SequenceNumber}, ts {hdr.Timestamp}, marker {hdr.MarkerBit}, payload {payload.Length}.");

                //var hdr = rtpPacket.Header;
                var frameStream = _h264Depacketiser.ProcessRTPPayload(payload, hdr.SequenceNumber, hdr.Timestamp, hdr.MarkerBit);

                if (frameStream != null)
                {
                    return frameStream.ToArray();
                }
            }
            else
            {
                logger.LogWarning($"rtp unknown video, seqnum {hdr.SequenceNumber}, ts {hdr.Timestamp}, marker {hdr.MarkerBit}, payload {payload.Length}.");
            }

            return null;
        }
    }
}
