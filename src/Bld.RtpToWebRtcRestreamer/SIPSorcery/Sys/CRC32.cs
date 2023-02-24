// from http://damieng.com/blog/2006/08/08/Calculating_CRC32_in_C_and_NET

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys
{
    public static class Crc32
    {
        private const UInt32 DefaultPolynomial = 0xedb88320;
        private const UInt32 DefaultSeed = 0xffffffff;

        private static UInt32[] _defaultTable;
        

        public static UInt32 Compute(byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, 0, buffer.Length);
        }
        
        private static UInt32[] InitializeTable(UInt32 polynomial)
        {
            if (polynomial == DefaultPolynomial && _defaultTable != null)
            {
                return _defaultTable;
            }

            UInt32[] createTable = new UInt32[256];
            for (int i = 0; i < 256; i++)
            {
                UInt32 entry = (UInt32)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                    {
                        entry = (entry >> 1) ^ polynomial;
                    }
                    else
                    {
                        entry = entry >> 1;
                    }
                }
                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
            {
                _defaultTable = createTable;
            }

            return createTable;
        }

        private static UInt32 CalculateHash(UInt32[] table, UInt32 seed, byte[] buffer, int start, int size)
        {
            UInt32 crc = seed;
            for (int i = start; i < size; i++)
            {
                unchecked
                {
                    crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
                }
            }
            return crc;
        }
    }
}
