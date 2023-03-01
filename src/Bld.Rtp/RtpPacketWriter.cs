namespace Bld.Rtp;

public static class RtpPacketWriter
{
    public static void WriteToBuffer(Span<byte> destination, RtpHeader header, ReadOnlySpan<byte> payload)
    {
        var headerBytes = RtpHeaderSerializer.GetBytes(header);
        headerBytes.CopyTo(destination);
        payload.CopyTo(destination[headerBytes.Length..]);
    }
}