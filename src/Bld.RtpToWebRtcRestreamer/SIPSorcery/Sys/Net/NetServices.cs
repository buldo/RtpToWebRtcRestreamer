#nullable enable
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

/// <summary>
/// Helper class to provide network services.
/// </summary>
public static class NetServices
{
    private const int RTP_RECEIVE_BUFFER_SIZE = 1000000;
    private const int RTP_SEND_BUFFER_SIZE = 1000000;

    /// <summary>
    /// The maximum number of re-attempts that will be made when trying to bind a UDP socket.
    /// </summary>
    private const int MAXIMUM_UDP_PORT_BIND_ATTEMPTS = 25;

    private static readonly ILogger logger = Log.Logger;

    /// <summary>
    /// A lookup collection to cache the local IP address for a destination address. The collection will cache results of
    /// asking the Operating System which local address to use for a destination address. The cache saves a relatively
    /// expensive call to create a socket and ask the OS for a route lookup.
    ///
    /// TODO:  Clear this cache if the state of the local network interfaces change.
    /// </summary>
    private static readonly ConcurrentDictionary<IPAddress, Tuple<IPAddress, DateTime>> LocalAddressTable = new();

    /// <summary>
    /// Checks whether an IP address can be used on the underlying System.
    /// </summary>
    /// <param name="bindAddress">The bind address to use.</param>
    private static void CheckBindAddressAndThrow(IPAddress bindAddress)
    {
        if (bindAddress is { AddressFamily: AddressFamily.InterNetworkV6 } && !Socket.OSSupportsIPv6)
        {
            throw new ApplicationException(
                "A UDP socket cannot be created on an IPv6 address due to lack of OS support.");
        }

        if (bindAddress is { AddressFamily: AddressFamily.InterNetwork } && !Socket.OSSupportsIPv4)
        {
            throw new ApplicationException(
                "A UDP socket cannot be created on an IPv4 address due to lack of OS support.");
        }
    }

    /// <summary>
    /// Attempts to create and bind a socket with defined protocol. The socket is always created with the ExclusiveAddressUse socket option
    /// set to accommodate a Windows 10 .Net Core socket bug where the same port can be bound to two different
    /// sockets, see https://github.com/dotnet/runtime/issues/36618.
    /// </summary>
    /// <returns>A bound socket if successful or throws an ApplicationException if unable to bind.</returns>
    private static Socket CreateBoundSocket(IPEndPoint ipEndPoint)
    {
        logger.LogDebug($"CreateBoundSocket attempting to create and bind socket(s) on {ipEndPoint} using protocol {ProtocolType.Udp}.");

        CheckBindAddressAndThrow(ipEndPoint.Address);

        var bindAttempts = 0;
        var addressFamily = ipEndPoint.AddressFamily;
        var success = false;
        Socket socket = null;

        while (bindAttempts < MAXIMUM_UDP_PORT_BIND_ATTEMPTS)
        {
            try
            {
                socket = CreateSocket(addressFamily);
                BindSocket(socket, ipEndPoint);

                if (addressFamily == AddressFamily.InterNetworkV6)
                {
                    logger.LogDebug(
                        $"CreateBoundSocket successfully bound on {socket.LocalEndPoint}, dual mode {socket.DualMode}.");
                }
                else
                {
                    logger.LogDebug($"CreateBoundSocket successfully bound on {socket.LocalEndPoint}.");
                }

                success = true;
            }
            catch (SocketException sockExcp)
            {
                if (sockExcp.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    // Try again if the port is already in use.
                    logger.LogWarning(
                        $"Address already in use exception attempting to bind socket, attempt {bindAttempts}.");
                    success = false;
                }
                else if (sockExcp.SocketErrorCode == SocketError.AccessDenied)
                {
                    // This exception seems to be interchangeable with address already in use. Perhaps a race condition with another process
                    // attempting to bind at the same time.
                    logger.LogWarning($"Access denied exception attempting to bind socket, attempt {bindAttempts}.");
                    success = false;
                }
                else
                {
                    logger.LogError($"SocketException in NetServices.CreateBoundSocket. {sockExcp}");
                    throw;
                }
            }
            catch (Exception excp)
            {
                logger.LogError(
                    $"Exception in NetServices.CreateBoundSocket attempting the initial socket bind on address {ipEndPoint}. {excp}");
                throw;
            }
            finally
            {
                if (!success)
                {
                    socket?.Close();
                }
            }

            if (success || ipEndPoint.Port != 0)
            {
                // If the bind was requested on a specific port there is no need to try again.
                break;
            }

            bindAttempts++;
        }

        if (success)
        {
            return socket;
        }

        throw new ApplicationException($"Unable to bind socket using end point {ipEndPoint}.");
    }

    private static void BindSocket(Socket socket, IPEndPoint ipEndpoint)
    {
        // Nasty code warning. On Windows Subsystem for Linux (WSL) on Windows 10
        // the OS lets a socket bind on an IPv6 dual mode port even if there
        // is an IPv4 socket bound to the same port. To prevent this occurring
        // a test IPv4 socket bind is carried out.
        // This happen even if the exclusive address socket option is set.
        // See https://github.com/dotnet/runtime/issues/36618.
        if (ipEndpoint.Port != 0 &&
            socket.AddressFamily == AddressFamily.InterNetworkV6 &&
            socket.DualMode && IPAddress.IPv6Any.Equals(ipEndpoint.Address) &&
            Environment.OSVersion.Platform == PlatformID.Unix &&
            RuntimeInformation.OSDescription.Contains("Microsoft"))
        {
            // Create a dummy IPv4 socket and attempt to bind it to the same port
            // to check the port isn't already in use.
            if (Socket.OSSupportsIPv4)
            {
                logger.LogDebug($"WSL detected, carrying out bind check on 0.0.0.0:{ipEndpoint.Port}.");

                using var testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                testSocket.Bind(new IPEndPoint(IPAddress.Any, ipEndpoint.Port));
                testSocket.Close();
            }
        }

        socket.Bind(ipEndpoint);
    }

    private static Socket CreateSocket(AddressFamily addressFamily)
    {
        var sock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
        sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

        if (addressFamily == AddressFamily.InterNetworkV6)
        {
            sock.DualMode = false;
        }

        return sock;
    }

    /// <summary>
    /// Attempts to create and bind a new RTP Socket with protocol, and optionally an control (RTCP), socket(s).
    /// The RTP and control sockets created are IPv4 and IPv6 dual mode sockets which means they can send and receive
    /// either IPv4 or IPv6 packets.
    /// </summary>
    public static Socket? CreateRtpSocket(IPEndPoint ipEndPoint)
    {
        CheckBindAddressAndThrow(ipEndPoint.Address);

        logger.LogDebug($"CreateRtpSocket attempting to create and bind RTP socket(s) on {ipEndPoint}.");

        Socket? rtpSocket = null;
        var bindAttempts = 0;

        while (bindAttempts < MAXIMUM_UDP_PORT_BIND_ATTEMPTS)
        {
            try
            {
                rtpSocket = CreateBoundSocket(ipEndPoint);
                rtpSocket.ReceiveBufferSize = RTP_RECEIVE_BUFFER_SIZE;
                rtpSocket.SendBufferSize = RTP_SEND_BUFFER_SIZE;
            }
            catch (ApplicationException)
            {
            }

            if (rtpSocket != null || ipEndPoint.Port != 0)
            {
                // If a specific bind port was specified only a single attempt to create the socket is made.
                break;
            }

            rtpSocket?.Close();
            bindAttempts++;
            rtpSocket = null;

            logger.LogWarning(
                $"CreateRtpSocket failed to create and bind RTP socket(s) on {ipEndPoint}, bind attempt {bindAttempts}.");
        }

        if (rtpSocket != null)
        {
            if (rtpSocket.LocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                logger.LogDebug(
                    $"Successfully bound RTP socket {rtpSocket.LocalEndPoint} (dual mode {rtpSocket.DualMode}).");
            }
            else
            {
                logger.LogDebug($"Successfully bound RTP socket {rtpSocket.LocalEndPoint}.");
            }
        }
        else
        {
            throw new ApplicationException($"Failed to create and bind RTP socket using bind address {ipEndPoint}.");
        }

        return rtpSocket;
    }

    /// <summary>
    /// Determines the local IP address to use to connection a remote address and
    /// returns all the local addresses (IPv4 and IPv6) that are bound to the same
    /// interface. The main (and probably sole) use case for this method is
    /// gathering host candidates for a WebRTC ICE session. Rather than selecting
    /// ALL local IP addresses only those on the interface needed to connect to
    /// the destination are returned.
    /// </summary>
    /// <returns>A list of local IP addresses on the identified interface(s).</returns>
    public static List<IPAddress> GetLocalAddressesOnInterface()
    {
        var localAddresses = new List<IPAddress>();

        var adapters = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var n in adapters)
        {
            // AC 5 Jun 2020: Network interface status is reported as Unknown on WSL.
            if (n.OperationalStatus == OperationalStatus.Up || n.OperationalStatus == OperationalStatus.Unknown)
            {
                var ipProps = n.GetIPProperties();
                localAddresses.AddRange(ipProps.UnicastAddresses.Select(x => x.Address));
            }
        }

        return localAddresses;
    }
}