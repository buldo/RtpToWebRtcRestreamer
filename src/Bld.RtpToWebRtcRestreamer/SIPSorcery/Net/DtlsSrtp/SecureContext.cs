//-----------------------------------------------------------------------------
// Filename: RtpSecureContextCollection.cs
//
// Description: Represents a secure context for Rtp Sessions
//
// Author(s):
// Jean-Philippe Fournier
//
// History:
// 5 January 2022 : Jean-Philippe Fournier, created Montréal, QC, Canada
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

internal delegate int ProtectRtpPacket(byte[] payload, int length, out int outputBufferLength);

internal class SecureContext
{
    public ProtectRtpPacket ProtectRtpPacket { get; }

    public ProtectRtpPacket ProtectRtcpPacket { get; }

    public ProtectRtpPacket UnprotectRtcpPacket { get; }

    public SecureContext(ProtectRtpPacket protectRtpPacket, ProtectRtpPacket protectRtcpPacket, ProtectRtpPacket unprotectRtcpPacket)
    {
        ProtectRtpPacket = protectRtpPacket;
        ProtectRtcpPacket = protectRtcpPacket;
        UnprotectRtcpPacket = unprotectRtcpPacket;
    }
}