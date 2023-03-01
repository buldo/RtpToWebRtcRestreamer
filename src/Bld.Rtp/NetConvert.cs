namespace Bld.Rtp;

internal static class NetConvert {
    public static UInt16 DoReverseEndian(UInt16 x) {
        //return Convert.ToUInt16((x << 8 & 0xff00) | (x >> 8));
        return BitConverter.ToUInt16(BitConverter.GetBytes(x).Reverse().ToArray(), 0);
    }

    public static uint DoReverseEndian(uint x) {
        //return (x << 24 | (x & 0xff00) << 8 | (x & 0xff0000) >> 8 | x >> 24);
        return BitConverter.ToUInt32(BitConverter.GetBytes(x).Reverse().ToArray(), 0);
    }
}