using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    internal class VideoStream : MediaStream
    {
        public VideoStream(RtpSessionConfig config, int index) : base(config, index)
        {
            MediaType = SDPMediaTypesEnum.video;
        }

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
    }
}
