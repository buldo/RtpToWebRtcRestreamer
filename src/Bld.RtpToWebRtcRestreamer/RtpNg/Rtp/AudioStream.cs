using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;

internal class AudioStream : MediaStream
{
    public AudioStream(RtpSessionConfig config, int index) : base(config, index)
    {
    }

    public override SDPMediaTypesEnum MediaType => SDPMediaTypesEnum.audio;

    public bool HasAudio
    {
        get
        {
            return LocalTrack != null && LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive;
        }
    }
}