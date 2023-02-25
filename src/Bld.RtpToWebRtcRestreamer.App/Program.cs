using System.Net;

namespace Bld.RtpToWebRtcRestreamer.App
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddRtpRestreamer(new IPEndPoint(IPAddress.Any, 5600), new IPEndPoint(IPAddress.Any, 8081));

            builder.Services.AddSignalR();

            builder.Services
                .AddControllersWithViews()
                .AddControllersAsServices();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
            }

            app.UseStaticFiles();
            app.UseRouting();


            app.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}");

            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}