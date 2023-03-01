using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Bld.Rtp.UdpSource
{
    public class PooledUdpSource
    {
        private const int MAX_UDP_SIZE = 0x10000;
        private readonly Action<RtpPacket> _receiveHandler;
        private readonly ILogger _logger;
        private readonly ArrayPool<byte> _receiveBuffersPool = ArrayPool<byte>.Shared;
        private readonly ObjectPool<RtpPacket> _packetsPool =
            new DefaultObjectPool<RtpPacket>(new DefaultPooledObjectPolicy<RtpPacket>(), 60);
        private readonly UdpClient _client;

        private Task? _receiveTask;
        private CancellationTokenSource? _cts;

        public PooledUdpSource(
            IPEndPoint listenEndPoint,
            Action<RtpPacket> receiveHandler,
            ILogger<PooledUdpSource> logger)
        {
            _receiveHandler = receiveHandler;
            _logger = logger;
            _client = new(listenEndPoint);
        }

        public void Start()
        {
            _cts = new();
            _receiveTask = Task.Run(async () => await ReceiveRoutine(_cts.Token), _cts.Token);
        }

        public async Task StopAsync()
        {
            if (_receiveTask != null)
            {
                _cts?.Cancel();
                await _receiveTask;
            }
        }

        public void ReusePacket(RtpPacket packet)
        {
            var buffer = packet.ReleaseBuffer();
            _receiveBuffersPool.Return(buffer);
            _packetsPool.Return(packet);
        }

        private async Task ReceiveRoutine(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var buffer = _receiveBuffersPool.Rent(MAX_UDP_SIZE);
                try
                {
                    var read = await _client.Client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                    if (read > 0)
                    {
                        var packet = _packetsPool.Get();
                        packet.ApplyBuffer(buffer, 0, read);
                        _receiveHandler(packet);
                    }
                    else
                    {
                        _receiveBuffersPool.Return(buffer);
                    }
                }
                catch (Exception exception)
                {
                    _receiveBuffersPool.Return(buffer);
                    _logger.LogError(exception, "Error");
                    throw;
                }
            }
        }
    }
}