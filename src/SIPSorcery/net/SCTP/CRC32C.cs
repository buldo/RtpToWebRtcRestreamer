﻿namespace SIPSorcery.Net
{
    public static class CRC32C
    {
        private const uint INITIAL_POLYNOMIAL = 0x82f63b78;

        private static readonly uint[] _table = new uint[256];

        static CRC32C()
        {
            uint poly = INITIAL_POLYNOMIAL;
            for (uint i = 0; i < 256; i++)
            {
                uint res = i;
                for (int k = 0; k < 8; k++)
                {
                    res = (res & 1) == 1 ? poly ^ (res >> 1) : (res >> 1);
                }
                _table[i] = res;
            }
        }

        public static uint Calculate(byte[] buffer, int offset, int length)
        {
            uint crc = ~0u;
            while (--length >= 0)
            {
                crc = _table[(crc ^ buffer[offset++]) & 0xff] ^ crc >> 8;
            }
            return crc ^ 0xffffffff;
        }
    }
}