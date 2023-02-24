using System.Net;
using Bld.RtpReceiver;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer
{
    public static class AppExtensions
    {
        /// <summary>
        /// Call AddSignalR and AddControllersAsServices first
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddRtpRestreamer(this IServiceCollection services, IPEndPoint rtpListenerEndpoint)
        {
            services.AddHostedService<WebRtcHostedService>();
            return services;
        }
    }
}