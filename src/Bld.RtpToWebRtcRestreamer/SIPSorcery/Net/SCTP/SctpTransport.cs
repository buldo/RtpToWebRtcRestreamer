//-----------------------------------------------------------------------------
// Filename: SctpTransport.cs
//
// Description: Represents a common SCTP transport layer.
//
// Remarks:
// The interface defined in https://tools.ietf.org/html/rfc4960#section-10
// was used as a basis for this class.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// St Patrick's Day 2021	Aaron Clauson	Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using SIPSorcery;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP;

/// <summary>
/// Contains the common methods that an SCTP transport layer needs to implement.
/// As well as being able to be carried directly in IP packets, SCTP packets can
/// also be wrapped in higher level protocols.
/// </summary>
internal abstract class SctpTransport
{
    private const int HMAC_KEY_SIZE = 64;

    /// <summary>
    /// As per https://tools.ietf.org/html/rfc4960#section-15.
    /// </summary>
    public const int DEFAULT_COOKIE_LIFETIME_SECONDS = 60;

    private static readonly ILogger logger = LogFactory.CreateLogger<SctpTransport>();

    /// <summary>
    /// Ephemeral secret key to use for generating cookie HMAC's. The purpose of the HMAC is
    /// to prevent resource depletion attacks. This does not justify using an external key store.
    /// </summary>
    private static readonly byte[] _hmacKey = new byte[HMAC_KEY_SIZE];

    /// <summary>
    /// This property can be used to indicate whether an SCTP transport layer is port agnostic.
    /// For example a DTLS transport is likely to only ever create a single SCTP association
    /// and the SCTP ports are redundant for matching end points. This allows the checks done
    /// on received SCTP packets to be more accepting about the ports used in the SCTP packet
    /// header.
    /// </summary>
    /// <returns>
    /// True if the transport implementation does not rely on the SCTP source and
    /// destination port for end point matching. False if it does.
    /// </returns>
    public virtual bool IsPortAgnostic => false;

    public abstract void Send(byte[] buffer, int offset, int length);

    static SctpTransport()
    {
        Random.Shared.NextBytes(_hmacKey);
    }

    protected void GotInit(SctpPacket initPacket, IPEndPoint remoteEndPoint)
    {
        // INIT packets have specific processing rules in order to prevent resource exhaustion.
        // See Section 5 of RFC 4960 https://tools.ietf.org/html/rfc4960#section-5 "Association Initialization".

        var initChunk = initPacket.Chunks.Single(x => x.KnownType == SctpChunkType.INIT) as SctpInitChunk;

        if (initChunk.InitiateTag == 0 ||
            initChunk.NumberInboundStreams == 0 ||
            initChunk.NumberOutboundStreams == 0)
        {
            // If the value of the Initiate Tag in a received INIT chunk is found
            // to be 0, the receiver MUST treat it as an error and close the
            // association by transmitting an ABORT. (RFC4960 pg. 25)

            // Note: A receiver of an INIT with the OS value set to 0 SHOULD
            // abort the association. (RFC4960 pg. 25)

            // Note: A receiver of an INIT with the MIS value of 0 SHOULD abort
            // the association. (RFC4960 pg. 26)

            SendError(
                true,
                initPacket.Header.DestinationPort,
                initPacket.Header.SourcePort,
                initChunk.InitiateTag,
                new SctpCauseOnlyError(SctpErrorCauseCode.InvalidMandatoryParameter));
        }
        else
        {
            var initAckPacket = GetInitAck(initPacket, remoteEndPoint);
            var buffer = initAckPacket.GetBytes();
            Send(buffer, 0, buffer.Length);
        }
    }

    /// <summary>
    /// Gets a cookie to send in an INIT ACK chunk. This method
    /// is overloadable so that different transports can tailor how the cookie
    /// is created. For example the WebRTC SCTP transport only ever uses a
    /// single association so the local Tag and TSN properties must be
    /// the same rather than random.
    /// </summary>
    protected virtual SctpTransportCookie GetInitAckCookie(
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
            Tag = Crypto.GetRandomUInt(),
            TSN = Crypto.GetRandomUInt(),
            ARwnd = SctpAssociation.DEFAULT_ADVERTISED_RECEIVE_WINDOW,
            CreatedAt = DateTime.Now.ToString("o"),
            Lifetime = DEFAULT_COOKIE_LIFETIME_SECONDS + lifeTimeExtension,
            HMAC = string.Empty
        };

        return cookie;
    }

    /// <summary>
    /// Creates the INIT ACK chunk and packet to send as a response to an SCTP
    /// packet containing an INIT chunk.
    /// </summary>
    /// <param name="initPacket">The received packet containing the INIT chunk.</param>
    /// <param name="remoteEP">Optional. The remote IP end point the INIT packet was
    /// received on. For transports that don't use an IP transport directly this parameter
    /// can be set to null and it will not form part of the COOKIE ECHO checks.</param>
    /// <returns>An SCTP packet with a single INIT ACK chunk.</returns>
    private SctpPacket GetInitAck(SctpPacket initPacket, IPEndPoint remoteEP)
    {
        var initChunk = initPacket.Chunks.Single(x => x.KnownType == SctpChunkType.INIT) as SctpInitChunk;

        var initAckPacket = new SctpPacket(
            initPacket.Header.DestinationPort,
            initPacket.Header.SourcePort,
            initChunk.InitiateTag);

        var cookie = GetInitAckCookie(
            initPacket.Header.DestinationPort,
            initPacket.Header.SourcePort,
            initChunk.InitiateTag,
            initChunk.InitialTSN,
            initChunk.ARwnd,
            remoteEP != null ? remoteEP.ToString() : string.Empty,
            (int)(initChunk.CookiePreservative / 1000));

        var jsonBuffer = JsonSerializer.SerializeToUtf8Bytes(cookie);

        using (var hmac = new HMACSHA256(_hmacKey))
        {
            var result = hmac.ComputeHash(jsonBuffer);
            cookie.HMAC = result.HexStr();
        }

        var jsonBufferWithHMAC = JsonSerializer.SerializeToUtf8Bytes(cookie);

        var initAckChunk = new SctpInitChunk(
            SctpChunkType.INIT_ACK,
            cookie.Tag,
            cookie.TSN,
            cookie.ARwnd,
            SctpAssociation.DEFAULT_NUMBER_OUTBOUND_STREAMS,
            SctpAssociation.DEFAULT_NUMBER_INBOUND_STREAMS);
        initAckChunk.StateCookie = jsonBufferWithHMAC;
        initAckChunk.UnrecognizedPeerParameters = initChunk.UnrecognizedPeerParameters;

        initAckPacket.AddChunk(initAckChunk);

        return initAckPacket;
    }

    /// <summary>
    /// Attempts to retrieve the cookie that should have been set by this peer from a COOKIE ECHO
    /// chunk. This is the step in the handshake that a new SCTP association will be created
    /// for a remote party. Providing the state cookie is valid create a new association.
    /// </summary>
    /// <param name="sctpPacket">The packet containing the COOKIE ECHO chunk received from the remote party.</param>
    /// <returns>If the state cookie in the chunk is valid a new SCTP association will be returned. IF
    /// it's not valid an empty cookie will be returned and an error response gets sent to the peer.</returns>
    protected SctpTransportCookie GetCookie(SctpPacket sctpPacket)
    {
        var cookieEcho = sctpPacket.Chunks.Single(x => x.KnownType == SctpChunkType.COOKIE_ECHO);
        var cookieBuffer = cookieEcho.ChunkValue;
        var cookie = JsonSerializer.Deserialize<SctpTransportCookie>(cookieBuffer);

        logger.LogDebug($"Cookie: {JsonSerializer.Serialize(cookie)}");

        var calculatedHMAC = GetCookieHMAC(cookieBuffer);
        if (calculatedHMAC != cookie.HMAC)
        {
            logger.LogWarning($"SCTP COOKIE ECHO chunk had an invalid HMAC, calculated {calculatedHMAC}, cookie {cookie.HMAC}.");
            SendError(
                true,
                sctpPacket.Header.DestinationPort,
                sctpPacket.Header.SourcePort,
                0,
                new SctpCauseOnlyError(SctpErrorCauseCode.InvalidMandatoryParameter));
            return SctpTransportCookie.Empty;
        }

        if (DateTime.Now.Subtract(DateTime.Parse(cookie.CreatedAt)).TotalSeconds > cookie.Lifetime)
        {
            logger.LogWarning($"SCTP COOKIE ECHO chunk was stale, created at {cookie.CreatedAt}, now {DateTime.Now.ToString("o")}, lifetime {cookie.Lifetime}s.");
            var diff = DateTime.Now.Subtract(DateTime.Parse(cookie.CreatedAt).AddSeconds(cookie.Lifetime));
            SendError(
                true,
                sctpPacket.Header.DestinationPort,
                sctpPacket.Header.SourcePort,
                0,
                new SctpErrorStaleCookieError { MeasureOfStaleness = (uint)(diff.TotalMilliseconds * 1000) });
            return SctpTransportCookie.Empty;
        }

        return cookie;
    }

    /// <summary>
    /// Checks whether the state cookie that is supplied in a COOKIE ECHO chunk is valid for
    /// this SCTP transport.
    /// </summary>
    /// <param name="buffer">The buffer holding the state cookie.</param>
    /// <returns>True if the cookie is determined as valid, false if not.</returns>
    private string GetCookieHMAC(byte[] buffer)
    {
        var cookie = JsonSerializer.Deserialize<SctpTransportCookie>(buffer);
        string hmacCalculated;
        cookie.HMAC = string.Empty;

        var cookiePreImage = JsonSerializer.SerializeToUtf8Bytes(cookie);

        using (var hmac = new HMACSHA256(_hmacKey))
        {
            var result = hmac.ComputeHash(cookiePreImage);
            hmacCalculated = result.HexStr();
        }

        return hmacCalculated;
    }

    /// <summary>
    /// Send an SCTP packet with one of the error type chunks (ABORT or ERROR) to the remote peer.
    /// </summary>
    /// <param name=isAbort">Set to true to use an ABORT chunk otherwise an ERROR chunk will be used.</param>
    /// <param name="desintationPort">The SCTP destination port.</param>
    /// <param name="sourcePort">The SCTP source port.</param>
    /// <param name="initiateTag">If available the initial tag for the remote peer.</param>
    /// <param name="error">The error to send.</param>
    private void SendError(
        bool isAbort,
        ushort destinationPort,
        ushort sourcePort,
        uint initiateTag,
        ISctpErrorCause error)
    {
        var errorPacket = new SctpPacket(
            destinationPort,
            sourcePort,
            initiateTag);

        var errorChunk = isAbort ? new SctpAbortChunk(true) : new SctpErrorChunk();
        errorChunk.AddErrorCause(error);
        errorPacket.AddChunk(errorChunk);

        var buffer = errorPacket.GetBytes();
        Send(buffer, 0, buffer.Length);
    }
}