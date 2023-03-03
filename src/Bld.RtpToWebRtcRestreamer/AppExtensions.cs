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
            services.AddSingleton<WebRtcHostedService>();
            services.AddHostedService<WebRtcHostedService>(provider => provider.GetRequiredService<WebRtcHostedService>());
            services.AddSingleton<IRtpRestreamerControl, RtpRestreamerControl>();
        }
    }
}