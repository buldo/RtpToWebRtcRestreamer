using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTCP
{
    /// <summary>
    /// Represents a point in time sample for a reception report.
    /// </summary>
    internal class ReceptionReportSample
    {
        public const int PAYLOAD_SIZE = 24;

        /// <summary>
        /// Data source being reported.
        /// </summary>
        public uint SSRC;

        /// <summary>
        /// Fraction lost since last SR/RR.
        /// </summary>
        private byte FractionLost;

        /// <summary>
        /// Cumulative number of packets lost (signed!).
        /// </summary>
        public int PacketsLost;

        /// <summary>
        /// Extended last sequence number received.
        /// </summary>
        private uint ExtendedHighestSequenceNumber;

        /// <summary>
        /// Interarrival jitter.
        /// </summary>
        public uint Jitter;

        /// <summary>
        /// Last SR packet from this source.
        /// </summary>
        private uint LastSenderReportTimestamp;

        /// <summary>
        /// Delay since last SR packet.
        /// </summary>
        private uint DelaySinceLastSenderReport;

        public ReceptionReportSample(byte[] packet)
        {
            if (BitConverter.IsLittleEndian)
            {
                SSRC = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 0));
                FractionLost = packet[4];
                PacketsLost = NetConvert.DoReverseEndian(BitConverter.ToInt32(new byte[] { 0x00, packet[5], packet[6], packet[7] }, 0));
                ExtendedHighestSequenceNumber = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 8));
                Jitter = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 12));
                LastSenderReportTimestamp = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 16));
                DelaySinceLastSenderReport = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 20));
            }
            else
            {
                SSRC = BitConverter.ToUInt32(packet, 4);
                FractionLost = packet[4];
                PacketsLost = BitConverter.ToInt32(new byte[] { 0x00, packet[5], packet[6], packet[7] }, 0);
                ExtendedHighestSequenceNumber = BitConverter.ToUInt32(packet, 8);
                Jitter = BitConverter.ToUInt32(packet, 12);
                LastSenderReportTimestamp = BitConverter.ToUInt32(packet, 16);
                LastSenderReportTimestamp = BitConverter.ToUInt32(packet, 20);
            }
        }

        /// <summary>
        /// Serialises the reception report block to a byte array.
        /// </summary>
        /// <returns>A byte array.</returns>
        public byte[] GetBytes()
        {
            var payload = new byte[24];

            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(SSRC)), 0, payload, 0, 4);
                payload[4] = FractionLost;
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(PacketsLost)), 1, payload, 5, 3);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(ExtendedHighestSequenceNumber)), 0, payload, 8, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(Jitter)), 0, payload, 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(LastSenderReportTimestamp)), 0, payload, 16, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(DelaySinceLastSenderReport)), 0, payload, 20, 4);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(SSRC), 0, payload, 0, 4);
                payload[4] = FractionLost;
                Buffer.BlockCopy(BitConverter.GetBytes(PacketsLost), 1, payload, 5, 3);
                Buffer.BlockCopy(BitConverter.GetBytes(ExtendedHighestSequenceNumber), 0, payload, 8, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(Jitter), 0, payload, 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(LastSenderReportTimestamp), 0, payload, 16, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(DelaySinceLastSenderReport), 0, payload, 20, 4);
            }

            return payload;
        }
    }
}