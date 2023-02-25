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
    }
}