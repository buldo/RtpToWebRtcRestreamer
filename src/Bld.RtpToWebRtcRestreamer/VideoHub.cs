using Microsoft.AspNetCore.SignalR;

namespace Bld.RtpToWebRtcRestreamer;

internal class VideoHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }
}