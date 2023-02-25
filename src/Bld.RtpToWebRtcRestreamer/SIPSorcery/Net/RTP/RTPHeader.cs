//-----------------------------------------------------------------------------
// Filename: RTPHeader.cs
//
// Description: RTP Header as defined in RFC3550.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 22 May 2005	Aaron Clauson	Created, Dublin, Ireland.
// 11 Aug 2019  Aaron Clauson   Added full license header.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Crypto;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
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
        public DateTime ReceivedTime;
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
        public RTPHeader(byte[] packet)
        {
            if (packet.Length < MIN_HEADER_LEN)
            {
                throw new ApplicationException("The packet did not contain the minimum number of bytes for an RTP header packet.");
            }

            var firstWord = BitConverter.ToUInt16(packet, 0);

            if (BitConverter.IsLittleEndian)
            {
                firstWord = NetConvert.DoReverseEndian(firstWord);
                SequenceNumber = NetConvert.DoReverseEndian(BitConverter.ToUInt16(packet, 2));
                Timestamp = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 4));
                SyncSource = NetConvert.DoReverseEndian(BitConverter.ToUInt32(packet, 8));
            }
            else
            {
                SequenceNumber = BitConverter.ToUInt16(packet, 2);
                Timestamp = BitConverter.ToUInt32(packet, 4);
                SyncSource = BitConverter.ToUInt32(packet, 8);
            }


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
                if (BitConverter.IsLittleEndian)
                {
                    ExtensionProfile = NetConvert.DoReverseEndian(BitConverter.ToUInt16(packet, 12 + 4 * CSRCCount));
                    headerExtensionLength += 2;
                    ExtensionLength = NetConvert.DoReverseEndian(BitConverter.ToUInt16(packet, 14 + 4 * CSRCCount));
                    headerExtensionLength += 2 + ExtensionLength * 4;
                }
                else
                {
                    ExtensionProfile = BitConverter.ToUInt16(packet, 12 + 4 * CSRCCount);
                    headerExtensionLength += 2;
                    ExtensionLength = BitConverter.ToUInt16(packet, 14 + 4 * CSRCCount);
                    headerExtensionLength += 2 + ExtensionLength * 4;
                }

                if (ExtensionLength > 0 && packet.Length >= (headerAndCSRCLength + 4 + ExtensionLength * 4))
                {
                    ExtensionPayload = new byte[ExtensionLength * 4];
                    Buffer.BlockCopy(packet, headerAndCSRCLength + 4, ExtensionPayload, 0, ExtensionLength * 4);
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

            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(firstWord)), 0, header, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(SequenceNumber)), 0, header, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(Timestamp)), 0, header, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(SyncSource)), 0, header, 8, 4);

                if (HeaderExtensionFlag == 1)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(ExtensionProfile)), 0, header, 12 + 4 * CSRCCount, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(ExtensionLength)), 0, header, 14 + 4 * CSRCCount, 2);
                }
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(firstWord), 0, header, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(SequenceNumber), 0, header, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(Timestamp), 0, header, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(SyncSource), 0, header, 8, 4);

                if (HeaderExtensionFlag == 1)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(ExtensionProfile), 0, header, 12 + 4 * CSRCCount, 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(ExtensionLength), 0, header, 14 + 4 * CSRCCount, 2);
                }
            }

            if (ExtensionLength > 0 && ExtensionPayload != null)
            {
                Buffer.BlockCopy(ExtensionPayload, 0, header, 16 + 4 * CSRCCount, ExtensionLength * 4);
            }

            return header;
        }
    }
}
