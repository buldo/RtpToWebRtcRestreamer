namespace Bld.RtpToWebRtcRestreamer;

public interface IRtpRestreamerControl
{
    Task<(Guid PeerId, string Sdp)> AppendClient();

    Task ProcessClientAnswerAsync(Guid peerId, string sdpString);

    void Start();

    void Stop();
}