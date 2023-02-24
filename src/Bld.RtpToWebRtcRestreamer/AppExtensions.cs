using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
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
            services.AddSingleton<IClientConnectionsHandler, ClientConnectionsHandler>();
            return services;
        }

        public static HubEndpointConventionBuilder MapRtpRestreamer(this IEndpointRouteBuilder endpoints, string pattern)
        {
            return endpoints.MapHub<VideoHub>(pattern);
        }
    }
}