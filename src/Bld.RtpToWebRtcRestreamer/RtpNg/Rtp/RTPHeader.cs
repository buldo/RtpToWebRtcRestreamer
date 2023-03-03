using System.Buffers.Binary;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Crypto;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp
{
    internal class RTPHeader
    {
        public const int MIN_HEADER_LEN = 12;

        private const int RTP_VERSION = 2;

        private int Version = RTP_VERSION;                       // 2 bits.
        private int PaddingFlag;                             // 1 bit.
        private int HeaderExtensionFlag;                     // 1 bit.
        private int CSRCCount;                               // 4 bits
        public int MarkerBit;                               // 1 bit.
        public int PayloadType;                             // 7 bits.
        public UInt16 SequenceNumber;                           // 16 bits.
        public uint Timestamp;                                  // 32 bits.
        public uint SyncSource;                                 // 32 bits.
        private UInt16 ExtensionProfile;                         // 16 bits.
        private UInt16 ExtensionLength;                          // 16 bits, length of the header extensions in 32 bit words.
        private byte[] ExtensionPayload;

        public int PayloadSize;
        private byte PaddingCount;

        public int Length
        {
            get { return MIN_HEADER_LEN + (CSRCCount * 4) + ((HeaderExtensionFlag == 0) ? 0 : 4 + (ExtensionLength * 4)); }
        }

        public RTPHeader()
        {
            SequenceNumber = Crypto.GetRandomUInt16();
            SyncSource = Crypto.GetRandomUInt();
            Timestamp = Crypto.GetRandomUInt();
        }

        /// <summary>
        /// Extract and load the RTP header from an RTP packet.
        /// </summary>
        /// <param name="packet"></param>
        public RTPHeader(ReadOnlySpan<byte> packet)
        {
            if (packet.Length < MIN_HEADER_LEN)
            {
                throw new ApplicationException("The packet did not contain the minimum number of bytes for an RTP header packet.");
            }

            var firstWord = BinaryPrimitives.ReadUInt16BigEndian(packet);
            SequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
            Timestamp = BinaryPrimitives.ReadUInt32BigEndian(packet[4..]);
            SyncSource = BinaryPrimitives.ReadUInt32BigEndian(packet[8..]);

            Version = firstWord >> 14;
            PaddingFlag = (firstWord >> 13) & 0x1;
            HeaderExtensionFlag = (firstWord >> 12) & 0x1;
            CSRCCount = (firstWord >> 8) & 0xf;

            MarkerBit = (firstWord >> 7) & 0x1;
            PayloadType = firstWord & 0x7f;

            var headerExtensionLength = 0;
            var headerAndCSRCLength = 12 + 4 * CSRCCount;

            if (HeaderExtensionFlag == 1 && (packet.Length >= (headerAndCSRCLength + 4)))
            {
                ExtensionProfile = BinaryPrimitives.ReadUInt16BigEndian(packet[(12 + 4 * CSRCCount)..]);
                headerExtensionLength += 2;
                ExtensionLength = BinaryPrimitives.ReadUInt16BigEndian(packet[(14 + 4 * CSRCCount)..]);
                headerExtensionLength += 2 + ExtensionLength * 4;

                if (ExtensionLength > 0 && packet.Length >= (headerAndCSRCLength + 4 + ExtensionLength * 4))
                {
                    ExtensionPayload = new byte[ExtensionLength * 4];
                    packet[(headerAndCSRCLength + 4)..].CopyTo(ExtensionPayload[(ExtensionLength * 4)..]);
                }
            }

            PayloadSize = packet.Length - (headerAndCSRCLength + headerExtensionLength);
            if (PaddingFlag == 1)
            {
                PaddingCount = packet[packet.Length - 1];
                if (PaddingCount < PayloadSize)//Prevent some protocol attacks
                {
                    PayloadSize -= PaddingCount;
                }
            }
        }

        public byte[] GetBytes()
        {
            var header = new byte[Length];

            var firstWord = Convert.ToUInt16(Version * 16384 + PaddingFlag * 8192 + HeaderExtensionFlag * 4096 + CSRCCount * 256 + MarkerBit * 128 + PayloadType);

            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0, 2), firstWord);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2, 2), SequenceNumber);
            BinaryPrimitives.TryWriteUInt32BigEndian(header.AsSpan(4, 4), Timestamp);
            BinaryPrimitives.TryWriteUInt32BigEndian(header.AsSpan(8, 4), SyncSource);

            if (HeaderExtensionFlag == 1)
            {
                BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(12 + 4 * CSRCCount, 2), ExtensionProfile);
                BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(14 + 4 * CSRCCount, 2), ExtensionLength);
            }

            if (ExtensionLength > 0 && ExtensionPayload != null)
            {
                Buffer.BlockCopy(ExtensionPayload, 0, header, 16 + 4 * CSRCCount, ExtensionLength * 4);
            }

            return header;
        }
    }
}
