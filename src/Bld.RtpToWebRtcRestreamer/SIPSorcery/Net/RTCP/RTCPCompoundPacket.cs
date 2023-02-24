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

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTCP
{
    /// <summary>
    /// Represents an RTCP compound packet consisting of 1 or more
    /// RTCP packets combined together in a single buffer. According to RFC3550 RTCP 
    /// transmissions should always have at least 2 RTCP packets (a sender/receiver report
    /// and an SDES report). This implementation does not enforce that constraint for
    /// received reports but does for sends.
    /// </summary>
    public class RTCPCompoundPacket
    {
        private static readonly ILogger logger = Log.Logger;

        public RTCPSenderReport SenderReport { get; private set; }
        public RTCPReceiverReport ReceiverReport { get; private set; }
        public RTCPSDesReport SDesReport { get; private set; }
        public RTCPBye Bye { get; set; }
        public RTCPFeedback Feedback { get; set; }

        public RTCPCompoundPacket(RTCPSenderReport senderReport, RTCPSDesReport sdesReport)
        {
            SenderReport = senderReport;
            SDesReport = sdesReport;
        }

        public RTCPCompoundPacket(RTCPReceiverReport receiverReport, RTCPSDesReport sdesReport)
        {
            ReceiverReport = receiverReport;
            SDesReport = sdesReport;
        }

        /// <summary>
        /// Creates a new RTCP compound packet from a serialised buffer.
        /// </summary>
        /// <param name="packet">The serialised RTCP compound packet to parse.</param>
        public RTCPCompoundPacket(byte[] packet)
        {
            var offset = 0;
            while (offset < packet.Length)
            {
                if (packet.Length - offset < RTCPHeader.HEADER_BYTES_LENGTH)
                {
                    // Not enough bytes left for a RTCP header.
                    break;
                }

                var buffer = packet.Skip(offset).ToArray();

                // The payload type field is the second byte in the RTCP header.
                var packetTypeID = buffer[1];
                switch (packetTypeID)
                {
                    case (byte)RTCPReportTypesEnum.SR:
                        SenderReport = new RTCPSenderReport(buffer);
                        var srLength = (SenderReport != null) ? SenderReport.GetBytes().Length : Int32.MaxValue;
                        offset += srLength;
                        break;
                    case (byte)RTCPReportTypesEnum.RR:
                        ReceiverReport = new RTCPReceiverReport(buffer);
                        var rrLength = (ReceiverReport != null) ? ReceiverReport.GetBytes().Length : Int32.MaxValue;
                        offset += rrLength;
                        break;
                    case (byte)RTCPReportTypesEnum.SDES:
                        SDesReport = new RTCPSDesReport(buffer);
                        var sdesLength = (SDesReport != null) ? SDesReport.GetBytes().Length : Int32.MaxValue;
                        offset += sdesLength;
                        break;
                    case (byte)RTCPReportTypesEnum.BYE:
                        Bye = new RTCPBye(buffer);
                        var byeLength = (Bye != null) ? Bye.GetBytes().Length : Int32.MaxValue;
                        offset += byeLength;
                        break;
                    case (byte)RTCPReportTypesEnum.RTPFB:
                        // TODO: Interpret Generic RTP feedback reports.
                        Feedback = new RTCPFeedback(buffer);
                        var rtpfbFeedbackLength = (Feedback != null) ? Feedback.GetBytes().Length : Int32.MaxValue;
                        offset += rtpfbFeedbackLength;
                        //var rtpfbHeader = new RTCPHeader(buffer);
                        //offset += rtpfbHeader.Length * 4 + 4;
                        break;
                    case (byte)RTCPReportTypesEnum.PSFB:
                        // TODO: Interpret Payload specific feedback reports.
                        Feedback = new RTCPFeedback(buffer);
                        var psfbFeedbackLength = (Feedback != null) ? Feedback.GetBytes().Length : Int32.MaxValue;
                        offset += psfbFeedbackLength;
                        //var psfbHeader = new RTCPHeader(buffer);
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
                sb.AppendLine($"SDES: SSRC={SDesReport.SSRC}, CNAME={SDesReport.CNAME}");
            }

            if (SenderReport != null)
            {
                var sr = SenderReport;
                sb.AppendLine($"Sender: SSRC={sr.SSRC}, PKTS={sr.PacketCount}, BYTES={sr.OctetCount}");
                if (sr.ReceptionReports != null)
                {
                    foreach (var rr in sr.ReceptionReports)
                    {
                        sb.AppendLine($" RR: SSRC={rr.SSRC}, LOST={rr.PacketsLost}, JITTER={rr.Jitter}");
                    }
                }
            }

            if (ReceiverReport != null)
            {
                var recv = ReceiverReport;
                sb.AppendLine($"Receiver: SSRC={recv.SSRC}");
                if (recv.ReceptionReports != null)
                {
                    foreach (var rr in recv.ReceptionReports)
                    {
                        sb.AppendLine($" RR: SSRC={rr.SSRC}, LOST={rr.PacketsLost}, JITTER={rr.Jitter}");
                    }
                }
            }

            return sb.ToString().TrimEnd('\n');
        }
    }
}
