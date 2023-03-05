namespace Bld.RtpToWebRtcRestreamer;

public interface IRtpRestreamerControl
{
    Task<string> AppendClient(string sdpOffer);

    void Start();

    void Stop();
}