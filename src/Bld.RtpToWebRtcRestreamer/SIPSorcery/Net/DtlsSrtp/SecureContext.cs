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

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp
{
    public class SecureContext
    {
        public ProtectRtpPacket ProtectRtpPacket { get; }
        public ProtectRtpPacket ProtectRtcpPacket { get; }

        public ProtectRtpPacket UnprotectRtpPacket { get; }
        public ProtectRtpPacket UnprotectRtcpPacket { get; }

        public SecureContext(ProtectRtpPacket protectRtpPacket, ProtectRtpPacket unprotectRtpPacket, ProtectRtpPacket protectRtcpPacket, ProtectRtpPacket unprotectRtcpPacket)
        {
            ProtectRtpPacket = protectRtpPacket;
            ProtectRtcpPacket = protectRtcpPacket;
            UnprotectRtpPacket = unprotectRtpPacket;
            UnprotectRtcpPacket = unprotectRtcpPacket;
        }
    }
}