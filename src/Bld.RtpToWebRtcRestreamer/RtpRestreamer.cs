using System.Net;
using Bld.RtpToWebRtcRestreamer.RtpReceiver;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

using WebSocketSharp.Server;

namespace Bld.RtpToWebRtcRestreamer
{
    internal class RtpRestreamer
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RtpRestreamer> _logger;
        private readonly WebSocketServer _webSocketServer;
        private readonly Receiver _receiver;
        private readonly StreamMultiplexer _streamMultiplexer;
        private readonly Task _periodicalManagementTask;
        private int _connectedClientsCount;

        public RtpRestreamer(
            IPEndPoint webSocketEndpoint,
            IPEndPoint rtpListenEndpoint,
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<RtpRestreamer>();

            _webSocketServer = new WebSocketServer(webSocketEndpoint.Address, webSocketEndpoint.Port, false);
            _webSocketServer.AddWebSocketService<WebRTCWebSocketPeer>("/", (peer) =>
            {
                //peer.SetLogger(loggerFactory.CreateLogger<WebRTCWebSocketPeer>());
                peer.CreatePeerConnection = CreatePeerConnection;
            });

            _receiver = new Receiver(rtpListenEndpoint, loggerFactory.CreateLogger<Receiver>());
            _streamMultiplexer = new StreamMultiplexer(_receiver, _loggerFactory.CreateLogger<StreamMultiplexer>());

            _periodicalManagementTask = BackgroundTask();
        }

        private int ConnectedClientsCount
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

        public event EventHandler<ConnectedClientsChangedEventArgs> ConnectedClientsChanged;

        public void Start()
        {
            _webSocketServer.Start();
            _receiver.Start();
        }

        private async Task<RTCPeerConnection> CreatePeerConnection()
        {
            var pc = new RTCPeerConnection();
            _streamMultiplexer.RegisterPeer(pc);

            var videoTrack = new MediaStreamTrack(
                new VideoFormat(VideoCodecsEnum.H264, 96),
                MediaStreamStatusEnum.SendRecv);
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
}

