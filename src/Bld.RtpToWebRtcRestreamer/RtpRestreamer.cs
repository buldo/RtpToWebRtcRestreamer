using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

using WebSocketSharp.Server;

namespace Bld.RtpToWebRtcRestreamer;

internal class RtpRestreamer
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RtpRestreamer> _logger;
    private readonly PooledUdpSource _receiver;
    private readonly StreamMultiplexer _streamMultiplexer;
    private readonly Task _periodicalManagementTask;
    private readonly Dictionary<Guid, RTCPeerConnection> _peers = new();

    private int _connectedClientsCount;

    public RtpRestreamer(
        IPEndPoint rtpListenEndpoint,
        ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<RtpRestreamer>();

        _receiver = new(rtpListenEndpoint, _loggerFactory.CreateLogger<PooledUdpSource>());
        _streamMultiplexer = new StreamMultiplexer(_loggerFactory.CreateLogger<StreamMultiplexer>());

        // TODO: reenable
        //_periodicalManagementTask = Task.Run(async () => await BackgroundTask().ConfigureAwait(false));
    }

    public event EventHandler<ConnectedClientsChangedEventArgs> ConnectedClientsChanged;

    public int ConnectedClientsCount
    {
        set
        {
            if (_connectedClientsCount != value)
            {
                _connectedClientsCount = value;
                ConnectedClientsChanged?.Invoke(this, new ConnectedClientsChangedEventArgs(value));
            }
        }
    }

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted = true;

        _receiver.Start(RtpProcessorAsync);
    }

    public async Task StopAsync()
    {
        if (!IsStarted)
        {
            return;
        }

        IsStarted = false;
        await _receiver.StopAsync();
    }

    public async Task<(Guid PeerId, string Sdp)> AppendClient()
    {
        var newPeer = CreatePeerConnection();

        var answer = newPeer.createOffer();
        var peerId = Guid.NewGuid();
        _peers.Add(peerId, newPeer);

        return (peerId, answer.sdp);
    }

    public async Task ProcessClientAnswerAsync(Guid peerId, string sdpString)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            var result = peer.setRemoteDescription(new RTCSessionDescriptionInit
            {
                sdp = sdpString,
                type = RTCSdpType.answer
            });
            _logger.LogDebug("setRemoteDescription result: {@result}", result);
        }
    }

    private async Task RtpProcessorAsync(RtpPacket packet)
    {
        await _streamMultiplexer.SendVideoPacketAsync(packet);
        _receiver.ReusePacket(packet);
    }

    private RTCPeerConnection CreatePeerConnection()
    {
        var pc = new RTCPeerConnection();
        _streamMultiplexer.RegisterPeer(pc);

        var videoTrack = new MediaStreamTrack(
            new VideoFormat(VideoCodecsEnum.H264, 96),
            MediaStreamStatusEnum.SendOnly)
        {
            //StreamStatus = MediaStreamStatusEnum.SendOnly
        };
        pc.AddTrack(videoTrack);

        pc.onconnectionstatechange += (state) =>
        {
            _logger.LogDebug("Peer connection state change to {state}.", state);

            if (state == RTCPeerConnectionState.connected)
            {
                _streamMultiplexer.StartPeerTransmit(pc);
            }
            else if (state == RTCPeerConnectionState.failed)
            {
                _streamMultiplexer.StopPeerTransmit(pc);
                pc.Close("ice disconnection");
            }
            else if (state == RTCPeerConnectionState.closed)
            {
                _streamMultiplexer.StopPeerTransmit(pc);
            }
        };

        // Diagnostics.
        pc.OnReceiveReport += (re, media, rr) =>
        {
            _logger.LogDebug($"RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}");
        };

        return pc;
    }

    private async Task BackgroundTask()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (true)
        {
            try
            {
                await timer.WaitForNextTickAsync();
                _streamMultiplexer.Cleanup();
                ConnectedClientsCount = _streamMultiplexer.ActiveStreamsCount;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Background worker error");
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }
}