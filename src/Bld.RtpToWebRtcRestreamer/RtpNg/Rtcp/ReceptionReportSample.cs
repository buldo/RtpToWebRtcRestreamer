using System.Buffers.Binary;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtcp;

/// <summary>
/// Represents a point in time sample for a reception report.
/// </summary>
internal class ReceptionReportSample
{
    public const int PAYLOAD_SIZE = 24;

    /// <summary>
    /// Fraction lost since last SR/RR.
    /// </summary>
    private readonly byte _fractionLost;

    /// <summary>
    /// Extended last sequence number received.
    /// </summary>
    private readonly uint _extendedHighestSequenceNumber;

    /// <summary>
    /// Last SR packet from this source.
    /// </summary>
    private readonly uint _lastSenderReportTimestamp;

    /// <summary>
    /// Delay since last SR packet.
    /// </summary>
    private readonly uint _delaySinceLastSenderReport = 0;

    public ReceptionReportSample(ReadOnlySpan<byte> packet)
    {
        {
            Ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet[4..]);
            _fractionLost = packet[4];
            PacketsLost = BinaryPrimitives.ReadInt32BigEndian(new byte[] { 0x00, packet[5], packet[6], packet[7] });
            _extendedHighestSequenceNumber = BinaryPrimitives.ReadUInt32BigEndian(packet[8..]);
            Jitter = BinaryPrimitives.ReadUInt32BigEndian(packet[12..]);
            _lastSenderReportTimestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[16..]);
            _lastSenderReportTimestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[20..]);
        }
    }

    /// <summary>
    /// Data source being reported.
    /// </summary>
    public uint Ssrc { get; }

    /// <summary>
    /// Interarrival jitter.
    /// </summary>
    public uint Jitter { get; }

    /// <summary>
    /// Cumulative number of packets lost (signed!).
    /// </summary>
    public int PacketsLost { get; }

    /// <summary>
    /// Serialises the reception report block to a byte array.
    /// </summary>
    /// <returns>A byte array.</returns>
    public byte[] GetBytes()
    {
        var payload = new byte[24];

        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(Ssrc)), 0, payload, 0, 4);
            payload[4] = _fractionLost;
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(PacketsLost)), 1, payload, 5, 3);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(_extendedHighestSequenceNumber)), 0, payload, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(Jitter)), 0, payload, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(_lastSenderReportTimestamp)), 0, payload, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(_delaySinceLastSenderReport)), 0, payload, 20, 4);
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes(Ssrc), 0, payload, 0, 4);
            payload[4] = _fractionLost;
            Buffer.BlockCopy(BitConverter.GetBytes(PacketsLost), 1, payload, 5, 3);
            Buffer.BlockCopy(BitConverter.GetBytes(_extendedHighestSequenceNumber), 0, payload, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Jitter), 0, payload, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_lastSenderReportTimestamp), 0, payload, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_delaySinceLastSenderReport), 0, payload, 20, 4);
        }

        return payload;
    }
}