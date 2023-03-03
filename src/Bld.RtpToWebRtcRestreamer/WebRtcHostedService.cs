#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer;

internal class WebRtcHostedService : IHostedService
{
    private readonly WebRtcConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly object _rtpRestreamerLock = new();
    private RtpRestreamer? _rtpRestreamer;

    public WebRtcHostedService(
        WebRtcConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartStreamer();
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
            _rtpRestreamer?.Stop();
        }
    }

    public void StartStreamer()
    {
        lock (_rtpRestreamerLock)
        {
            if (_rtpRestreamer == null)
            {
                _rtpRestreamer = new RtpRestreamer(
                    _configuration.WebSocketListenEndpoint,
                    _configuration.RtpListenEndpoint,
                    _loggerFactory
                );
                _rtpRestreamer.ConnectedClientsChanged += RtpRestreamerOnConnectedClientsChanged;
            }
        }

        _rtpRestreamer.Start();
    }

    public void StopStreamer()
    {
        _rtpRestreamer?.Stop();
    }
}