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

        [HttpPost("start")]
        public void Start()
        {
            _control.Start();
        }

        [HttpPost("sdp")]
        [Produces("application/sdp")]
        [Consumes("application/sdp")]
        public async Task Post()
        {
            using var streamReader = new StreamReader(Request.Body, Encoding.UTF8);
            var sdpString = await streamReader.ReadToEndAsync();

            var answer = await _control.AppendClient(sdpString);

            Response.StatusCode = StatusCodes.Status201Created;
            await Response.WriteAsync(answer, Encoding.UTF8);
        }
    }
}