namespace SIPSorcery.Net
{
    public static class SctpPadding
    {
        public static ushort PadTo4ByteBoundary(int val)
        {
            return (ushort)(val % 4 == 0 ? val : val + 4 - val % 4);
        }
    }
}