using System.Text;

using Microsoft.AspNetCore.Mvc;

namespace Bld.RtpToWebRtcRestreamer.App.Controllers
{
    [Route("api/video")]
    [ApiController]
    public class VideoController : ControllerBase
    {
        private readonly IRtpRestreamerControl _control;

        public VideoController(IRtpRestreamerControl control)
        {
            _control = control;
        }

        [HttpPost("stop")]
        public async Task Stop()
        {
            await _control.StopAsync();
        }

        [HttpPost("sdp")]
        [Produces("application/sdp")]
        [Consumes("application/sdp")]
        public async Task Post()
        {
            using var streamReader = new StreamReader(Request.Body, Encoding.UTF8);
            var sdpString = await streamReader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(sdpString))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var answer = await _control.AppendClient();

            Response.StatusCode = StatusCodes.Status201Created;
            Response.Headers.Location = $"api/video/sdp/{answer.PeerId}";
            await Response.WriteAsync(answer.Sdp, Encoding.UTF8);
        }

        [HttpPatch("sdp/{peerId}")]
        [Consumes("application/sdp")]
        public async Task Patch([FromRoute] Guid peerId)
        {
            using var streamReader = new StreamReader(Request.Body, Encoding.UTF8);
            var sdpString = await streamReader.ReadToEndAsync();

            await _control.ProcessClientAnswerAsync(peerId, sdpString);

            Response.StatusCode = StatusCodes.Status200OK;
        }
    }
}