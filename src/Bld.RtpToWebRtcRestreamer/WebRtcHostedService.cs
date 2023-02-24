using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer
{
    internal class WebRtcHostedService : IHostedService
    {
        private readonly RtpRestreamer? _rtpRestreamer;

        public WebRtcHostedService(
            ILoggerFactory loggerFactory)
        {
            
                _rtpRestreamer = new RtpRestreamer(
                    new IPEndPoint(IPAddress.Any, 8081),
                    new IPEndPoint(IPAddress.Any, 5600),
                    loggerFactory
                );
                _rtpRestreamer.ConnectedClientsChanged += RtpRestreamerOnConnectedClientsChanged;
            
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _rtpRestreamer?.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void RtpRestreamerOnConnectedClientsChanged(object? sender, ConnectedClientsChangedEventArgs e)
        {
            if (e.NewCount == 0)
            {
                // Ensure port mirror stopped
            }
            else
            {
                // Ensure port mirror started
            }
        }
    }
}
