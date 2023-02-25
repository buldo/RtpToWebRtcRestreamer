using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Bld.RtpToWebRtcRestreamer
{
    public static class AppExtensions
    {
        public static void AddRtpRestreamer(
            this IServiceCollection services,
            IPEndPoint rtpListenEndpoint,
            IPEndPoint webSocketEndPoint)
        {
            var config = new WebRtcConfiguration
            {
                RtpListenEndpoint = rtpListenEndpoint,
                WebSocketListenEndpoint = webSocketEndPoint
            };

            services.AddSingleton(config);
            services.AddHostedService<WebRtcHostedService>();
            services.AddSingleton<IRtpRestreamerControl, RtpRestreamerControl>();
        }
    }
}