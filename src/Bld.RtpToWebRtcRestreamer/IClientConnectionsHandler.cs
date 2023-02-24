namespace Bld.RtpToWebRtcRestreamer;

public interface IClientConnectionsHandler
{
    Task ProcessNewClientAsync(string sdp);
}