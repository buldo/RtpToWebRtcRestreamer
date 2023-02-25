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
        public static void AddRtpRestreamer(this IServiceCollection services)
        {
            services.AddHostedService<WebRtcHostedService>();
        }
    }
}