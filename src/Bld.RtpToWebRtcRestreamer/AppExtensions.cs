using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Bld.RtpToWebRtcRestreamer
{
    public static class AppExtensions
    {
        /// <summary>
        /// Call AddSignalR and AddControllersAsServices first
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddRtpRestreamer(this IServiceCollection services)
        {
            services.AddHostedService<WebRtcHostedService>();
            return services;
        }
    }
}