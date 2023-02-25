//-----------------------------------------------------------------------------
// Filename: ReceptionReport.cs
//
// Description: One or more reception report blocks are included in each
// RTCP Sender and Receiver report.

//
//        RTCP Reception Report Block
//        0                   1                   2                   3
//        0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
// report |                 SSRC_1(SSRC of first source)                  |
// block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//  1     | fraction lost |       cumulative number of packets lost       |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |           extended highest sequence number received           |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                      interarrival jitter                      |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                         last SR(LSR)                          |
//        +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
//        |                   delay since last SR(DLSR)                   |
//        +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 29 Dec 2019  Aaron Clauson   Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTCP
{
    /// <summary>
    /// Maintains the reception statistics for a received RTP stream.
    /// </summary>
    internal class ReceptionReport
    {
        private const int RTPSeqMod = 1 << 16;
        private const int SeqNumWrapLow = 256;
        private const int SeqNumWrapHigh = 65280;

        /// <summary>
        /// Data source being reported.
        /// </summary>
        private readonly uint _ssrc;

        /// <summary>
        /// highest seq. number seen
        /// </summary>
        private ushort _mMaxSeq;

        /// <summary>
        /// Increments by UInt16.MaxValue each time the sequence number wraps around.
        /// </summary>
        private ulong _mCycles;

        /// <summary>
        /// The first sequence number received.
        /// </summary>
        private uint _mBaseSeq;

        /// <summary>
        /// packets received.
        /// </summary>
        private uint _mReceived;

        /// <summary>
        /// packet expected at last interval.
        /// </summary>
        private ulong _mExpectedPrior;

        /// <summary>
        /// packet received at last interval.
        /// </summary>
        private uint _mReceivedPrior;

        /// <summary>
        /// relative trans time for prev pkt.
        /// </summary>
        private uint _mTransit;

        /// <summary>
        /// Estimated jitter.
        /// </summary>
        private uint _mJitter;

        /// <summary>
        /// Received last SR packet timestamp.
        /// </summary>
        private ReceivedSRTimestamp _mReceivedLsrTimestamp;

        /// <summary>
        /// Creates a new Reception Report object.
        /// </summary>
        /// <param name="ssrc">The synchronisation source this reception report is for.</param>
        public ReceptionReport(uint ssrc)
        {
            _ssrc = ssrc;
        }

        /// <summary>
        /// Updates the state when an RTCP sender report is received from the remote party.
        /// </summary>
        /// <param name="srNtpTimestamp">The sender report timestamp.</param>
        internal void RtcpSenderReportReceived(ulong srNtpTimestamp)
        {
            Interlocked.Exchange(ref _mReceivedLsrTimestamp,
                new ReceivedSRTimestamp
                {
                    NTP = (uint)((srNtpTimestamp >> 16) & 0xFFFFFFFF),
                    ReceivedAt = DateTime.Now
                });
        }

        /// <summary>
        /// Carries out the calculations required to measure properties related to the reception of
        /// received RTP packets. The algorithms employed are:
        ///  - RFC3550 A.1 RTP Data Header Validity Checks (for sequence number calculations).
        ///  - RFC3550 A.3 Determining Number of Packets Expected and Lost.
        ///  - RFC3550 A.8 Estimating the Interarrival Jitter.
        /// </summary>
        /// <param name="seq">The sequence number in the RTP header.</param>
        /// <param name="rtpTimestamp">The timestamp in the RTP header.</param>
        /// <param name="arrivalTimestamp">The current timestamp in the SAME units as the RTP timestamp.
        /// For example for 8Khz audio the arrival timestamp needs 8000 ticks per second.</param>
        internal void RtpPacketReceived(ushort seq, uint rtpTimestamp, uint arrivalTimestamp)
        {
            if (_mReceived == 0)
            {
                _mBaseSeq = seq;
            }

            _mReceived++;

            if (seq == _mMaxSeq + 1)
            {
                // Packet is in sequence.
                _mMaxSeq = seq;
            }
            else if (seq == 0 && _mMaxSeq == ushort.MaxValue)
            {
                // Packet is in sequence and a wrap around has occurred.
                _mMaxSeq = seq;
                _mCycles += RTPSeqMod;
            }
            else
            {
                // Out of order, duplicate or skipped sequence number.
                if (seq > _mMaxSeq)
                {
                    // Seqnum is greater than expected. RTP packet is dropped or out of order.
                    _mMaxSeq = seq;
                }
                else if (seq < SeqNumWrapLow && _mMaxSeq > SeqNumWrapHigh)
                {
                    // Seqnum is out of order and has wrapped.
                    _mMaxSeq = seq;
                    _mCycles += RTPSeqMod;
                }
            }

            // Estimating the Interarrival Jitter as defined in RFC3550 Appendix A.8.
            var transit = arrivalTimestamp - rtpTimestamp;
            var d = (int)(transit - _mTransit);
            _mTransit = transit;
            if (d < 0)
            {
                d = -d;
            }
            _mJitter += (uint)(d - ((_mJitter + 8) >> 4));
        }

        /// <summary>
        /// Gets a point in time sample for the reception report.
        /// </summary>
        /// <returns>A reception report sample.</returns>
        public ReceptionReportSample GetSample(uint ntpTimestampNow)
        {
            // Determining the number of packets expected and lost in RFC3550 Appendix A.3.
            var extendedMax = _mCycles + _mMaxSeq;
            var expected = extendedMax - _mBaseSeq + 1;
            //int lost = (m_received == 0) ? 0 : (int)(expected - m_received);

            var expectedInterval = expected - _mExpectedPrior;
            _mExpectedPrior = expected;
            var receivedInterval = _mReceived - _mReceivedPrior;
            _mReceivedPrior = _mReceived;
            var lostInterval = (_mReceived == 0) ? 0 : expectedInterval - receivedInterval;
            var fraction = (byte)((expectedInterval == 0 || lostInterval <= 0) ? 0 : (lostInterval << 8) / expectedInterval);

            // In this case, the estimate is sampled for the reception report as:
            var jitter = _mJitter >> 4;

            var receivedLsrTimestamp = _mReceivedLsrTimestamp;
            var delay = receivedLsrTimestamp == null || receivedLsrTimestamp.ReceivedAt == DateTime.MinValue ?
                0 : ntpTimestampNow - RTCPSession.DateTimeToNtpTimestamp32(receivedLsrTimestamp.ReceivedAt);

            return new ReceptionReportSample(_ssrc, fraction, (int)lostInterval, _mMaxSeq, jitter, receivedLsrTimestamp?.NTP ?? 0, delay);
        }
    }
}
