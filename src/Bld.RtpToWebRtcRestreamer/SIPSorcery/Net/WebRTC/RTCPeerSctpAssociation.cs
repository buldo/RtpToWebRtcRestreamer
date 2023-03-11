//-----------------------------------------------------------------------------
// Filename: RTCPeerSctpAssociation.cs
//
// Description: Represents an SCTP association on top of the DTLS
// transport. Each peer connection only requires a single SCTP
// association. Multiple data channels can be created on top
// of the association.
//
// Remarks:
//
// - RFC8831 "WebRTC Data Channels" https://tools.ietf.org/html/rfc8831
//   Provides overview of WebRTC data channels and describes the DTLS +
//   SCTP infrastructure required.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 20 Jul 2020	Aaron Clauson	Created.
// 22 Mar 2021  Aaron Clauson   Refactored for new SCTP implementation.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

internal class RTCPeerSctpAssociation : SctpAssociation
{
    // TODO: Add MTU path discovery.
    private const ushort DEFAULT_DTLS_MTU = 1200;

    private static readonly ILogger logger = Log.Logger;

    /// <summary>
    /// Creates a new SCTP association with the remote peer.
    /// </summary>
    /// <param name="rtcSctpTransport">The DTLS transport that will be used to encapsulate the
    /// SCTP packets.</param>
    /// <param name="isClient">True if this peer will be the client within the association. This
    /// dictates whether streams created use odd or even ID's.</param>
    /// <param name="srcPort">The source port to use when forming the association.</param>
    /// <param name="dstPort">The destination port to use when forming the association.</param>
    /// <param name="dtlsPort">Optional. The local UDP port being used for the DTLS connection. This
    /// will be set on the SCTP association to aid in diagnostics.</param>
    public RTCPeerSctpAssociation(RTCSctpTransport rtcSctpTransport, ushort srcPort, ushort dstPort, int dtlsPort)
        : base(rtcSctpTransport, srcPort, dstPort, DEFAULT_DTLS_MTU, dtlsPort)
    {
        logger.LogDebug($"SCTP creating DTLS based association, is DTLS client {rtcSctpTransport.IsDtlsClient}, ID {ID}.");
    }
}