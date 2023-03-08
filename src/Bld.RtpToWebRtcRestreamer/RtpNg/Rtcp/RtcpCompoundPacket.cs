//-----------------------------------------------------------------------------
// Filename: RTCPCompoundPacket.cs
//
// Description: Represents an RTCP compound packet consisting of 1 or more
// RTCP packets combined together in a single buffer.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 30 Dec 2019	Aaron Clauson	Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// Represents an RTCP compound packet consisting of 1 or more
/// RTCP packets combined together in a single buffer. According to RFC3550 RTCP
/// transmissions should always have at least 2 RTCP packets (a sender/receiver report
/// and an SDES report). This implementation does not enforce that constraint for
/// received reports but does for sends.
/// </summary>
internal class RtcpCompoundPacket
{
    private static readonly ILogger logger = Log.Logger;

    public RtcpSenderReport SenderReport { get; }
    public RtcpReceiverReport ReceiverReport { get; }
    private RtcpSDesReport SDesReport { get; }
    public RtcpBye Bye { get; set; }
    public RtcpFeedback Feedback { get; }

    public RtcpCompoundPacket(RtcpSenderReport senderReport, RtcpSDesReport sdesReport)
    {
        SenderReport = senderReport;
        SDesReport = sdesReport;
    }

    public RtcpCompoundPacket(RtcpReceiverReport receiverReport, RtcpSDesReport sdesReport)
    {
        ReceiverReport = receiverReport;
        SDesReport = sdesReport;
    }

    /// <summary>
    /// Creates a new RTCP compound packet from a serialised buffer.
    /// </summary>
    /// <param name="packet">The serialised RTCP compound packet to parse.</param>
    public RtcpCompoundPacket(ReadOnlySpan<byte> packet)
    {
        var offset = 0;
        while (offset < packet.Length)
        {
            if (packet.Length - offset < RtcpHeader.HEADER_BYTES_LENGTH)
            {
                // Not enough bytes left for a RTCP header.
                break;
            }

            var buffer = packet.Slice(offset);

            // The payload type field is the second byte in the RTCP header.
            var packetTypeID = buffer[1];
            switch (packetTypeID)
            {
                case (byte)RtcpReportTypes.SR:
                    SenderReport = new RtcpSenderReport(buffer);
                    var srLength = (SenderReport != null) ? SenderReport.GetBytes().Length : Int32.MaxValue;
                    offset += srLength;
                    break;
                case (byte)RtcpReportTypes.RR:
                    ReceiverReport = new RtcpReceiverReport(buffer);
                    var rrLength = (ReceiverReport != null) ? ReceiverReport.GetBytes().Length : Int32.MaxValue;
                    offset += rrLength;
                    break;
                case (byte)RtcpReportTypes.SDES:
                    SDesReport = new RtcpSDesReport(buffer);
                    var sdesLength = (SDesReport != null) ? SDesReport.GetBytes().Length : Int32.MaxValue;
                    offset += sdesLength;
                    break;
                case (byte)RtcpReportTypes.BYE:
                    Bye = new RtcpBye(buffer);
                    var byeLength = (Bye != null) ? Bye.GetBytes().Length : Int32.MaxValue;
                    offset += byeLength;
                    break;
                case (byte)RtcpReportTypes.RTPFB:
                    // TODO: Interpret Generic RTP feedback reports.
                    Feedback = new RtcpFeedback(buffer);
                    var rtpfbFeedbackLength = (Feedback != null) ? Feedback.GetBytes().Length : Int32.MaxValue;
                    offset += rtpfbFeedbackLength;
                    //var rtpfbHeader = new RtcpHeader(buffer);
                    //offset += rtpfbHeader.Length * 4 + 4;
                    break;
                case (byte)RtcpReportTypes.PSFB:
                    // TODO: Interpret Payload specific feedback reports.
                    Feedback = new RtcpFeedback(buffer);
                    var psfbFeedbackLength = (Feedback != null) ? Feedback.GetBytes().Length : Int32.MaxValue;
                    offset += psfbFeedbackLength;
                    //var psfbHeader = new RtcpHeader(buffer);
                    //offset += psfbHeader.Length * 4 + 4;
                    break;
                default:
                    logger.LogWarning($"RTCPCompoundPacket did not recognise packet type ID {packetTypeID}.");
                    offset = Int32.MaxValue;
                    logger.LogWarning(packet.HexStr());
                    break;
            }
        }
    }

    public string GetDebugSummary()
    {
        var sb = new StringBuilder();

        if (Bye != null)
        {
            sb.AppendLine("BYE");
        }

        if (SDesReport != null)
        {
            sb.AppendLine($"SDES: SSRC={SDesReport.Ssrc}, CNAME={SDesReport.Cname}");
        }

        if (SenderReport != null)
        {
            var sr = SenderReport;
            sb.AppendLine($"Sender: SSRC={sr.Ssrc}, PKTS={sr.PacketCount}, BYTES={sr.OctetCount}");
            if (sr.ReceptionReports != null)
            {
                foreach (var rr in sr.ReceptionReports)
                {
                    sb.AppendLine($" RR: SSRC={rr.Ssrc}, LOST={rr.PacketsLost}, JITTER={rr.Jitter}");
                }
            }
        }

        if (ReceiverReport != null)
        {
            var recv = ReceiverReport;
            sb.AppendLine($"Receiver: SSRC={recv.Ssrc}");
            if (recv.ReceptionReports != null)
            {
                foreach (var rr in recv.ReceptionReports)
                {
                    sb.AppendLine($" RR: SSRC={rr.Ssrc}, LOST={rr.PacketsLost}, JITTER={rr.Jitter}");
                }
            }
        }

        return sb.ToString().TrimEnd('\n');
    }
}