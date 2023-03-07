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
    private RtpRestreamer? _rtpRestreamer;

    public WebRtcHostedService(
        WebRtcConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        LogFactory.Set(loggerFactory);
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

    public void StartStreamer()
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

        _rtpRestreamer.Start();
    }

    public void StopStreamer()
    {
        _rtpRestreamer?.Stop();
    }

    public async Task<(Guid PeerId, string Sdp)> AppendClient()
    {
        return await _rtpRestreamer.AppendClient();
    }

    public async Task ProcessClientAnswerAsync(Guid peerId, string sdpString)
    {
        await _rtpRestreamer.ProcessClientAnswerAsync(peerId, sdpString);
    }

    private void RtpRestreamerOnConnectedClientsChanged(object? sender, ConnectedClientsChangedEventArgs e)
    {
        //if (e.NewCount == 0)
        //{
        //    _rtpRestreamer?.Stop();
        //}
    }

}