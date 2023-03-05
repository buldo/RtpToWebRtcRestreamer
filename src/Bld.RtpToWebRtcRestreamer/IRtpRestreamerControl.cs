namespace Bld.RtpToWebRtcRestreamer;

public interface IRtpRestreamerControl
{
    Task<string> AppendClient();

    void Start();

    void Stop();
}