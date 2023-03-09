//-----------------------------------------------------------------------------
// Filename: RTPSessionConfig.cs
//
// Description: Represents an RTP session constituted of a single media stream. The session
// does not control the sockets as they may be shared by multiple sessions.
//
// Author(s):
// Kurt Kießling
//
// History:
// 30 Jul 2021  Kurt Kießling   Created.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal sealed class RtpSessionConfig
{
    /// <summary>
    /// If true RTCP reports will be multiplexed with RTP on a single channel.
    /// If false (standard mode) then a separate socket is used to send and receive RTCP reports.
    /// </summary>
    public bool IsRtcpMultiplexed { get; set; }

    /// <summary>
    /// Select type of secure media to use.
    /// </summary>
    public RtpSecureMediaOptionEnum RtpSecureMediaOption { get; set; }

    /// <summary>
    /// Optional. If specified this address will be used as the bind address for any RTP
    /// and control sockets created. Generally this address does not need to be set. The default behaviour
    /// is to bind to [::] or 0.0.0.0,d depending on system support, which minimises network routing
    /// causing connection issues.
    /// </summary>
    public IPAddress BindAddress { get; set; }

    /// <summary>
    /// Optional. If specified a single attempt will be made to bind the RTP socket
    /// on this port. It's recommended to leave this parameter as the default of 0 to let the Operating
    /// System select the port number.
    /// </summary>
    public int BindPort { get; set; }

    public bool IsSecure { get => RtpSecureMediaOption == RtpSecureMediaOptionEnum.DtlsSrtp; }

    public bool UseSdpCryptoNegotiation { get => RtpSecureMediaOption == RtpSecureMediaOptionEnum.SdpCryptoNegotiation; }
}