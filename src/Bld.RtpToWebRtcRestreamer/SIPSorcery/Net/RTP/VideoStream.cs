using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal class VideoStream : MediaStream
{
    public VideoStream(RtpSessionConfig config, int index) : base(config, index)
    {
    }

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

    public override SDPMediaTypesEnum MediaType => SDPMediaTypesEnum.video;
}