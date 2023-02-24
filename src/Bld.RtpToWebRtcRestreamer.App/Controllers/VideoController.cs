using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Bld.RtpToWebRtcRestreamer.App.Controllers;

[Route("api/video")]
[ApiController]
public class VideoController : ControllerBase
{
    private readonly IClientConnectionsHandler _clientConnectionsHandler;

    public VideoController(IClientConnectionsHandler clientConnectionsHandler)
    {
        _clientConnectionsHandler = clientConnectionsHandler;
    }

    [HttpPost]
    [Consumes("application/sdp")]
    public async Task Post()
    {
        using StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
        var sdp = await reader.ReadToEndAsync();
        await _clientConnectionsHandler.ProcessNewClientAsync(sdp);
    }
}