namespace Bld.RtpToWebRtcRestreamer;

internal class RtpRestreamerControl :IRtpRestreamerControl
{
    private readonly WebRtcHostedService _service;

    public RtpRestreamerControl(WebRtcHostedService service)
    {
        _service = service;
    }

    public void Start()
    {
        _service.StartStreamer();
    }

    public void Stop()
    {
        _service.StopStreamer();
    }
}