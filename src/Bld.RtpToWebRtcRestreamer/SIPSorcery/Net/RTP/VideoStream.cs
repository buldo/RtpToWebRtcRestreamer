//-----------------------------------------------------------------------------
// Filename: VideoStream.cs
//
// Description: Define a Video media stream (which inherits MediaStream) to focus an Video specific treatment
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

using Bld.RtpToWebRtcRestreamer.Common;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    internal class VideoStream : MediaStream
    {
        /// <summary>
        /// Gets fired when the remote SDP is received and the set of common video formats is set.
        /// </summary>
        public event Action<int, List<VideoFormat>> OnVideoFormatsNegotiatedByIndex;

        /// <summary>
        /// Indicates whether this session is using video.
        /// </summary>
        public bool HasVideo
        {
            get
            {
                return LocalTrack != null && LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive;
            }
        }

        private void SendH264Frame(RTPPacket packet)
        {
            if (CheckIfCanSendRtpRaw())
            {
                SendRtpRawFromPacket(packet);
            }
        }

        public void SendVideo(RTPPacket packet)
        {
            var videoSendingFormat = GetSendingFormat();

            switch (videoSendingFormat.Name()) {
                case "H264":
                    SendH264Frame(packet);
                    break;
                default:
                    throw new ApplicationException($"Unsupported video format selected {videoSendingFormat.Name()}.");
            }
        }

        public void CheckVideoFormatsNegotiation()
        {
            if (LocalTrack != null && LocalTrack.Capabilities?.Count() > 0)
            {
                OnVideoFormatsNegotiatedByIndex?.Invoke(
                            Index,
                            LocalTrack.Capabilities
                            .Select(x => x.ToVideoFormat()).ToList());
            }
        }

        public VideoStream(RtpSessionConfig config, int index) : base(config, index)
        {
            MediaType = SDPMediaTypesEnum.video;
        }
    }
}
