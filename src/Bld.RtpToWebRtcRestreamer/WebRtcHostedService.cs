#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SIPSorcery;

namespace Bld.RtpToWebRtcRestreamer;

internal class WebRtcHostedService : IHostedService
{
    private readonly WebRtcConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly object _rtpRestreamerLock = new();
    private readonly ILogger<WebRtcHostedService> _logger;
    private RtpRestreamer? _rtpRestreamer;

    public WebRtcHostedService(
        WebRtcConfiguration configuration,
        ILogger<WebRtcHostedService> logger,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;
        LogFactory.Set(loggerFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopStreamerAsync();
    }

    public async Task StopStreamerAsync()
    {
        if (_rtpRestreamer != null)
        {
            await _rtpRestreamer.StopAsync();
        }
    }

    public async Task<(Guid PeerId, string Sdp)> AppendClient()
    {
        lock (_rtpRestreamerLock)
        {
            if (_rtpRestreamer == null)
            {
                _rtpRestreamer = new RtpRestreamer(
                    _configuration.RtpListenEndpoint,
                    _loggerFactory
                );
                _rtpRestreamer.ConnectedClientsChanged += RtpRestreamerOnConnectedClientsChanged;
            }
        }

        if (!_rtpRestreamer.IsStarted)
        {
            _rtpRestreamer.Start();
        }

        return await _rtpRestreamer.AppendClient();
    }

    public async Task ProcessClientAnswerAsync(Guid peerId, string sdpString)
    {
        if (_rtpRestreamer == null)
        {
            return;
        }

        await _rtpRestreamer.ProcessClientAnswerAsync(peerId, sdpString);
    }

    private void RtpRestreamerOnConnectedClientsChanged(object? sender, ConnectedClientsChangedEventArgs e)
    {
        if (e.NewCount == 0)
        {
            try
            {
                _rtpRestreamer?.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Stop error");
            }
        }
    }
}