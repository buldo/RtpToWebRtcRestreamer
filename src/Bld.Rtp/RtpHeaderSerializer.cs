using System.Buffers.Binary;

namespace Bld.Rtp;

internal static class RtpHeaderSerializer
{
    internal const int MIN_HEADER_LEN = 12;

    /// <summary>
    /// Extract and load the RTP header from an RTP packet.
    /// </summary>
    /// <param name="packet"></param>
    public static RtpHeader Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < MIN_HEADER_LEN)
        {
            throw new ApplicationException(
                "The packet did not contain the minimum number of bytes for an RTP header packet.");
        }

        var header = new RtpHeader();

        var firstWord = BinaryPrimitives.ReadUInt16BigEndian(packet);
        header.SequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
        header.Timestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[4..]);
        header.SyncSource = BinaryPrimitives.ReadUInt32BigEndian(packet[8..]);

        header.Version = firstWord >> 14;
        header.PaddingFlag = (firstWord >> 13) & 0x1;
        header.HeaderExtensionFlag = (firstWord >> 12) & 0x1;
        header.CSRCCount = (firstWord >> 8) & 0xf;
        header.MarkerBit = (firstWord >> 7) & 0x1;
        header.PayloadType = firstWord & 0x7f;

        var headerExtensionLength = 0;
        var headerAndCSRCLength = 12 + 4 * header.CSRCCount;

        if (header.HeaderExtensionFlag == 1 && (packet.Length >= (headerAndCSRCLength + 4)))
        {
            header.ExtensionProfile = BinaryPrimitives.ReadUInt16BigEndian(packet[(12 + 4 * header.CSRCCount)..]);
            headerExtensionLength += 2;
            header.ExtensionLength = BinaryPrimitives.ReadUInt16BigEndian(packet[(14 + 4 * header.CSRCCount)..]);
            headerExtensionLength += 2 + header.ExtensionLength * 4;

            if (header.ExtensionLength > 0 && packet.Length >= (headerAndCSRCLength + 4 + header.ExtensionLength * 4))
            {
                header.ExtensionPayload = new byte[header.ExtensionLength * 4];
                packet[(headerAndCSRCLength + 4)..].CopyTo(header.ExtensionPayload[(header.ExtensionLength * 4)..]);
            }
        }

        header.PayloadSize = packet.Length - (headerAndCSRCLength + headerExtensionLength);
        if (header.PaddingFlag == 1)
        {
            header.PaddingCount = packet[packet.Length - 1];
            if (header.PaddingCount < header.PayloadSize) //Prevent some protocol attacks
            {
                header.PayloadSize -= header.PaddingCount;
            }
        }

        return header;
    }

    public static byte[] GetBytes(RtpHeader originalHeader)
    {
        var header = new byte[originalHeader.Length];

        var firstWord = Convert.ToUInt16(originalHeader.Version * 16384 + originalHeader.PaddingFlag * 8192 +
                                         originalHeader.HeaderExtensionFlag * 4096 +
                                         originalHeader.CSRCCount * 256 + originalHeader.MarkerBit * 128 +
                                         originalHeader.PayloadType);

        if (BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(firstWord)), 0, header, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(originalHeader.SequenceNumber)), 0,
                header, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(originalHeader.Timestamp)), 0, header, 4,
                4);
            Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(originalHeader.SyncSource)), 0, header, 8,
                4);

            if (originalHeader.HeaderExtensionFlag == 1)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(originalHeader.ExtensionProfile)), 0,
                    header,
                    12 + 4 * originalHeader.CSRCCount, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(originalHeader.ExtensionLength)), 0,
                    header,
                    14 + 4 * originalHeader.CSRCCount, 2);
            }
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes(firstWord), 0, header, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(originalHeader.SequenceNumber), 0, header, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(originalHeader.Timestamp), 0, header, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(originalHeader.SyncSource), 0, header, 8, 4);

            if (originalHeader.HeaderExtensionFlag == 1)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(originalHeader.ExtensionProfile), 0, header,
                    12 + 4 * originalHeader.CSRCCount, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(originalHeader.ExtensionLength), 0, header,
                    14 + 4 * originalHeader.CSRCCount, 2);
            }
        }

        if (originalHeader.ExtensionLength > 0 && originalHeader.ExtensionPayload != null)
        {
            Buffer.BlockCopy(originalHeader.ExtensionPayload, 0, header, 16 + 4 * originalHeader.CSRCCount,
                originalHeader.ExtensionLength * 4);
        }

        return header;
    }
}