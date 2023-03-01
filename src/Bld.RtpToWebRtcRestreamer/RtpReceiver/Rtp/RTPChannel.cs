using System.Net;
using System.Net.Sockets;
using Bld.Rtp;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;

/// <summary>
/// A communications channel for transmitting and receiving Real-time Protocol (RTP) and
/// Real-time Control Protocol (RTCP) packets. This class performs the socket management
/// functions.
/// </summary>
internal sealed class RTPChannel : IDisposable
{
    private readonly ILogger _logger;
    private readonly Socket _rtpSocket;
    private UdpReceiver _rtpReceiver;
    private bool _rtpReceiverStarted;
    private bool _isClosed;

    /// <summary>
    /// Creates a new RTP channel. The RTP and optionally RTCP sockets will be bound in the constructor.
    /// They do not start receiving until the Start method is called.
    /// </summary>
    public RTPChannel(
        IPAddress bindAddress,
        int bindPort,
        ILogger logger)
    {
        _logger = logger;
        NetServices.CreateRtpSocket(false, bindAddress, bindPort, out var rtpSocket, out _);

        _rtpSocket = rtpSocket ?? throw new ApplicationException("The RTP channel was not able to create an RTP socket.");
    }


    public event Action<RtpPacket> OnRtpDataReceived;

    /// <summary>
    /// Starts listening on the RTP and control ports.
    /// </summary>
    public void Start()
    {
        if (!_rtpReceiverStarted)
        {
            _rtpReceiverStarted = true;

            _logger.LogDebug($"RTPChannel for {_rtpSocket.LocalEndPoint} started.");

            _rtpReceiver = new UdpReceiver(_rtpSocket);
            _rtpReceiver.OnPacketReceived += OnRTPPacketReceived;
            _rtpReceiver.OnClosed += Close;
            _rtpReceiver.BeginReceiveFrom();
        }
    }

    /// <summary>
    /// Closes the session's RTP and control ports.
    /// </summary>
    private void Close(string reason)
    {
        if (!_isClosed)
        {
            try
            {
                _isClosed = true;
                _rtpReceiver?.Close(reason ?? "normal");
            }
            catch (Exception exception)
            {
                _logger.LogError("Exception RTPChannel.Close. " + exception);
            }
        }
    }

    /// <summary>
    /// Event handler for packets received on the RTP UDP socket.
    /// </summary>
    private void OnRTPPacketReceived(UdpReceiver receiver, RtpPacket packet)
    {
        OnRtpDataReceived?.Invoke(packet);
    }

    public void Dispose()
    {
        Close(null);
    }
}