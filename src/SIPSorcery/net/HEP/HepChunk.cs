using System;
using System.Net;
using System.Net.Sockets;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    public class HepChunk
    {
        private const ushort GENERIC_VENDOR_ID = 0x0000;  // Vendor ID for the default chunk types.
        private const ushort MINIMUM_CHUNK_LENGTH = 6;

        /// <summary>
        /// Creates the initial buffer for the HEP packet and sets the vendor, chunk type ID and length fields.
        /// Note: Vendor ID could change and make endianess relevant.
        /// </summary>
        /// <param name="chunkType">The chunk type to set in the serialised chunk.</param>
        /// <param name="length">The value to set in the length field of the serialised chunk.</param>
        /// <returns>A buffer that contains the serialised chunk EXCEPT for the payload.</returns>
        private static byte[] InitBuffer(ChunkTypeEnum chunkType, ushort length)
        {
            byte[] buf = new byte[length];
            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(GENERIC_VENDOR_ID)), 0, buf, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian((ushort)chunkType)), 0, buf, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(length)), 0, buf, 4, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(GENERIC_VENDOR_ID), 0, buf, 0, 2);
                Buffer.BlockCopy(BitConverter.GetBytes((ushort)chunkType), 0, buf, 2, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(length), 0, buf, 4, 2);
            }
            return buf;
        }

        /// <summary>
        /// Gets the chunk bytes for a single byte chunk type.
        /// </summary>
        public static byte[] GetBytes(ChunkTypeEnum chunkType, byte val)
        {
            byte[] buf = InitBuffer(chunkType, MINIMUM_CHUNK_LENGTH + 1);
            buf[MINIMUM_CHUNK_LENGTH] = val;
            return buf;
        }

        /// <summary>
        /// Gets the chunk bytes for an unsigned short chunk type.
        /// </summary>
        public static byte[] GetBytes(ChunkTypeEnum chunkType, ushort val)
        {
            byte[] buf = InitBuffer(chunkType, MINIMUM_CHUNK_LENGTH + 2);

            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(val)), 0, buf, MINIMUM_CHUNK_LENGTH, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(val), 0, buf, MINIMUM_CHUNK_LENGTH, 2);
            }
            return buf;
        }

        /// <summary>
        /// Gets the chunk bytes for an unsigned int chunk type.
        /// </summary>
        public static byte[] GetBytes(ChunkTypeEnum chunkType, uint val)
        {
            byte[] buf = InitBuffer(chunkType, MINIMUM_CHUNK_LENGTH + 4);

            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian(val)), 0, buf, MINIMUM_CHUNK_LENGTH, 4);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(val), 0, buf, MINIMUM_CHUNK_LENGTH, 4);
            }
            return buf;
        }

        /// <summary>
        /// Gets the chunk bytes for an arbitrary payload.
        /// </summary>
        public static byte[] GetBytes(ChunkTypeEnum chunkType, byte[] payload)
        {
            byte[] buf = InitBuffer(chunkType, (ushort)(MINIMUM_CHUNK_LENGTH + payload.Length));
            Buffer.BlockCopy(payload, 0, buf, MINIMUM_CHUNK_LENGTH, (ushort)payload.Length);
            return buf;
        }

        /// <summary>
        /// Gets the chunk bytes for IP address type chunks.
        /// </summary>
        public static byte[] GetBytes(ChunkTypeEnum chunkType, IPAddress address)
        {
            if (chunkType == ChunkTypeEnum.IPv4SourceAddress || chunkType == ChunkTypeEnum.IPv4DesinationAddress)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ApplicationException("Incorrect IP address family suppled to HepChunk.");
                }

                byte[] buf = InitBuffer(chunkType, MINIMUM_CHUNK_LENGTH + 4);
                Buffer.BlockCopy(address.GetAddressBytes(), 0, buf, MINIMUM_CHUNK_LENGTH, 4);
                return buf;
            }
            else if (chunkType == ChunkTypeEnum.IPv6SourceAddress || chunkType == ChunkTypeEnum.IPv6DesinationAddress)
            {
                if (address.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    throw new ApplicationException("Incorrect IP address family suppled to HepChunk.");
                }

                byte[] buf = InitBuffer(chunkType, MINIMUM_CHUNK_LENGTH + 16);
                Buffer.BlockCopy(address.GetAddressBytes(), 0, buf, MINIMUM_CHUNK_LENGTH, 16);
                return buf;
            }
            else
            {
                throw new ApplicationException("IP address HepChunk does not support the chunk type.");
            }
        }
    }
}