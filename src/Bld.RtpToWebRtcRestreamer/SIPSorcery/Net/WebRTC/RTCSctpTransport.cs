//-----------------------------------------------------------------------------
// Filename: RTCSctpTransport.cs
//
// Description: Represents a DTLS based transport for sending and receiving
// SCTP packets. This transport in turn forms the base for WebRTC data
// channels.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 22 Mar 2021	Aaron Clauson	Created.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net.Sockets;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Tls;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
///     Represents an SCTP transport that uses a DTLS transport.
/// </summary>
/// <remarks>
///     DTLS encapsulation of SCTP:
///     https://tools.ietf.org/html/rfc8261
///     WebRTC API RTCSctpTransport Interface definition:
///     https://www.w3.org/TR/webrtc/#webidl-1410933428
/// </remarks>
internal class RTCSctpTransport : SctpTransport
{
    private const string THREAD_NAME_PREFIX = "rtcsctprecv-";

    /// <summary>
    ///     The DTLS transport has no mechanism to cancel a pending receive. The workaround is
    ///     to set a timeout on each receive call.
    /// </summary>
    private const int RECEIVE_TIMEOUT_MILLISECONDS = 1000;

    /// <summary>
    ///     The default maximum size of payload that can be sent on a data channel.
    /// </summary>
    /// <remarks>
    ///     https://www.w3.org/TR/webrtc/#sctp-transport-update-mms
    /// </remarks>
    private const uint SCTP_DEFAULT_MAX_MESSAGE_SIZE = 262144;

    private static readonly ILogger logger = Log.Logger;

    private readonly RTCPeerSctpAssociation _rtcSctpAssociation;
    private bool _isClosed;

    private bool _isStarted;
    private Thread _receiveThread;

    /// <summary>
    ///     The transport over which all SCTP packets for data channels
    ///     will be sent and received.
    /// </summary>
    private DatagramTransport _transport;

    /// <summary>
    ///     Creates a new SCTP transport that runs on top of an established DTLS connection.
    /// </summary>
    /// <param name="sourcePort">The SCTP source port.</param>
    /// <param name="destinationPort">The SCTP destination port.</param>
    /// <param name="dtlsPort">
    ///     Optional. The local UDP port being used for the DTLS connection. This
    ///     will be set on the SCTP association to aid in diagnostics.
    /// </param>
    public RTCSctpTransport(ushort sourcePort, ushort destinationPort, int dtlsPort)
    {
        State = RTCSctpTransportState.Closed;

        _rtcSctpAssociation = new RTCPeerSctpAssociation(this, sourcePort, destinationPort, dtlsPort);
        _rtcSctpAssociation.OnAssociationStateChanged += OnAssociationStateChanged;
    }

    /// <summary>
    ///     The SCTP ports are redundant for a DTLS transport. There will only ever be one
    ///     SCTP association so the SCTP ports do not need to be used for end point matching.
    /// </summary>
    public override bool IsPortAgnostic => true;

    /// <summary>
    ///     Indicates the role of this peer in the DTLS connection. This influences
    ///     the selection of stream ID's for SCTP messages.
    /// </summary>
    public bool IsDtlsClient { get; private set; }

    /// <summary>
    ///     The current state of the SCTP transport.
    /// </summary>
    public RTCSctpTransportState State { get; private set; }

    /// <summary>
    ///     Attempts to update the SCTP destination port the association managed by this transport will use.
    /// </summary>
    /// <param name="port">The updated destination port.</param>
    public void UpdateDestinationPort(ushort port)
    {
        if (State != RTCSctpTransportState.Closed)
        {
            logger.LogWarning($"SCTP destination port cannot be updated when the transport is in state {State}.");
        }
        else
        {
            _rtcSctpAssociation.UpdateDestinationPort(port);
        }
    }

    /// <summary>
    ///     Starts the SCTP transport receive thread.
    /// </summary>
    public void Start(DatagramTransport dtlsTransport, bool isDtlsClient)
    {
        if (!_isStarted)
        {
            _isStarted = true;

            _transport = dtlsTransport;
            IsDtlsClient = isDtlsClient;

            _receiveThread = new Thread(DoReceive);
            _receiveThread.Name = $"{THREAD_NAME_PREFIX}{_rtcSctpAssociation.ID}";
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }
    }

    /// <summary>
    ///     Closes the SCTP association and stops the receive thread.
    /// </summary>
    public void Close()
    {
        if (State == RTCSctpTransportState.Connected)
        {
            _rtcSctpAssociation?.Shutdown();
        }

        _isClosed = true;
    }

    /// <summary>
    ///     Event handler to coordinate changes to the SCTP association state with the overall
    ///     SCTP transport state.
    /// </summary>
    /// <param name="associationState">The state of the SCTP association.</param>
    private void OnAssociationStateChanged(SctpAssociationState associationState)
    {
        if (associationState == SctpAssociationState.Established)
        {
            State = RTCSctpTransportState.Connected;
        }
        else if (associationState == SctpAssociationState.Closed)
        {
            State = RTCSctpTransportState.Closed;
        }
    }

    /// <summary>
    ///     Gets a cookie to send in an INIT ACK chunk. This SCTP
    ///     transport for a WebRTC peer connection needs to use the same
    ///     local tag and TSN in every chunk as only a single association
    ///     is ever maintained.
    /// </summary>
    protected override SctpTransportCookie GetInitAckCookie(
        ushort sourcePort,
        ushort destinationPort,
        uint remoteTag,
        uint remoteTSN,
        uint remoteARwnd,
        string remoteEndPoint,
        int lifeTimeExtension = 0)
    {
        var cookie = new SctpTransportCookie
        {
            SourcePort = sourcePort,
            DestinationPort = destinationPort,
            RemoteTag = remoteTag,
            RemoteTSN = remoteTSN,
            RemoteARwnd = remoteARwnd,
            RemoteEndPoint = remoteEndPoint,
            Tag = _rtcSctpAssociation.VerificationTag,
            ARwnd = SctpAssociation.DEFAULT_ADVERTISED_RECEIVE_WINDOW,
            CreatedAt = DateTime.Now.ToString("o"),
            Lifetime = DEFAULT_COOKIE_LIFETIME_SECONDS + lifeTimeExtension,
            HMAC = string.Empty
        };

        return cookie;
    }

    /// <summary>
    ///     This method runs on a dedicated thread to listen for incoming SCTP
    ///     packets on the DTLS transport.
    /// </summary>
    private void DoReceive(object state)
    {
        var recvBuffer = new byte[SctpAssociation.DEFAULT_ADVERTISED_RECEIVE_WINDOW];

        while (!_isClosed)
        {
            try
            {
                var bytesRead = _transport.Receive(recvBuffer, 0, recvBuffer.Length, RECEIVE_TIMEOUT_MILLISECONDS);

                if (bytesRead == DtlsSrtpTransport.DtlsRetransmissionCode)
                {
                    // Timed out waiting for a packet, this is by design and the receive attempt should
                    // be retired.
                    continue;
                }

                if (bytesRead > 0)
                {
                    if (!SctpPacket.VerifyChecksum(recvBuffer, 0, bytesRead))
                    {
                        logger.LogWarning("SCTP packet received on DTLS transport dropped due to invalid checksum.");
                    }
                    else
                    {
                        var pkt = SctpPacket.Parse(recvBuffer, 0, bytesRead);

                        if (pkt.Chunks.Any(x => x.KnownType == SctpChunkType.INIT))
                        {
                            var initChunk = pkt.Chunks.First(x => x.KnownType == SctpChunkType.INIT) as SctpInitChunk;
                            logger.LogDebug(
                                $"SCTP INIT packet received, initial tag {initChunk.InitiateTag}, initial TSN {initChunk.InitialTSN}.");

                            GotInit(pkt, null);
                        }
                        else if (pkt.Chunks.Any(x => x.KnownType == SctpChunkType.COOKIE_ECHO))
                        {
                            // The COOKIE ECHO chunk is the 3rd step in the SCTP handshake when the remote party has
                            // requested a new association be created.
                            var cookie = GetCookie(pkt);

                            if (cookie.IsEmpty())
                            {
                                logger.LogWarning("SCTP error acquiring handshake cookie from COOKIE ECHO chunk.");
                            }
                            else
                            {
                                _rtcSctpAssociation.GotCookie(cookie);

                                if (pkt.Chunks.Count() > 1)
                                {
                                    // There could be DATA chunks after the COOKIE ECHO chunk.
                                    _rtcSctpAssociation.OnPacketReceived(pkt);
                                }
                            }
                        }
                        else
                        {
                            _rtcSctpAssociation.OnPacketReceived(pkt);
                        }
                    }
                }
                else if (_isClosed)
                {
                    // The DTLS transport has been closed or is no longer available.
                    logger.LogWarning("SCTP the RTCSctpTransport DTLS transport returned an error.");
                    break;
                }
            }
            catch (ApplicationException appExcp)
            {
                // Treat application exceptions as recoverable, things like SCTP packet parse failures.
                logger.LogWarning($"SCTP error processing RTCSctpTransport receive. {appExcp.Message}");
            }
            catch (TlsFatalAlert alert) when (alert.InnerException is SocketException)
            {
                var sockExcp = alert.InnerException as SocketException;
                logger.LogWarning($"SCTP RTCSctpTransport receive socket failure {sockExcp.SocketErrorCode}.");
                break;
            }
            catch (Exception excp)
            {
                logger.LogError($"SCTP fatal error processing RTCSctpTransport receive. {excp}");
                break;
            }
        }

        if (!_isClosed)
        {
            logger.LogWarning($"SCTP association {_rtcSctpAssociation.ID} receive thread stopped.");
        }

        State = RTCSctpTransportState.Closed;
    }

    /// <summary>
    ///     This method is called by the SCTP association when it wants to send an SCTP packet
    ///     to the remote party.
    /// </summary>
    /// <param name="buffer">The buffer containing the data to send.</param>
    /// <param name="offset">The position in the buffer to send from.</param>
    /// <param name="length">The number of bytes to send.</param>
    public override void Send(byte[] buffer, int offset, int length)
    {
        if (length > SCTP_DEFAULT_MAX_MESSAGE_SIZE)
        {
            throw new ApplicationException($"RTCSctpTransport was requested to send data of length {length} " +
                                           $" that exceeded the maximum allowed message size of {SCTP_DEFAULT_MAX_MESSAGE_SIZE}.");
        }

        if (!_isClosed)
        {
            lock (_transport)
            {
                _transport.Send(buffer, offset, length);
            }
        }
    }
}