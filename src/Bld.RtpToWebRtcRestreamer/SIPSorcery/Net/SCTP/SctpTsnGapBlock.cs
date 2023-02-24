namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP
{
    public struct SctpTsnGapBlock
    {
        /// <summary>
        /// Indicates the Start offset TSN for this Gap Ack Block.  To
        /// calculate the actual TSN number the Cumulative TSN Ack is added to
        /// this offset number.This calculated TSN identifies the first TSN
        /// in this Gap Ack Block that has been received.
        /// </summary>
        public ushort Start;

        /// <summary>
        /// Indicates the End offset TSN for this Gap Ack Block.  To calculate
        /// the actual TSN number, the Cumulative TSN Ack is added to this
        /// offset number.This calculated TSN identifies the TSN of the last
        /// DATA chunk received in this Gap Ack Block.
        /// </summary>
        public ushort End;
    }
}