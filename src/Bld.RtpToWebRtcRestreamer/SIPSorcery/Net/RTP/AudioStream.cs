using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal class AudioStream : MediaStream
{
    /// <summary>
    /// Indicates whether this session is using audio.
    /// </summary>
    public bool HasAudio
    {
        get
        {
            return LocalTrack != null && LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive;
        }
    }

    public AudioStream(RtpSessionConfig config, int index) : base(config, index)
    {
        MediaType = SDPMediaTypesEnum.audio;
    }
}