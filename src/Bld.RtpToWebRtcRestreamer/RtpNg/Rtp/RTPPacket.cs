#nullable enable
namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;

internal class RtpPacket
{
    private byte[]? _dataBuffer;
    private Memory<byte> _rawPacket;
    private RtpHeader? _header;
    private Memory<byte> _payload;

    public bool IsReadyToUse { get; private set; }

    public RtpHeader Header
    {
        get
        {
            if (!IsReadyToUse)
            {
                throw new Exception("RtpPacket has no data");
            }

            return _header!;
        }
    }

    public ReadOnlySpan<byte> Payload
    {
        get
        {
            if (!IsReadyToUse)
            {
                throw new Exception("RtpPacket has no data");
            }

            return _payload.Span;
        }
    }

    public void ApplyBuffer(byte[] data, int start, int length)
    {
        if (IsReadyToUse)
        {
            throw new Exception("RtpPacket already handle data");
        }

        _dataBuffer = data;
        _rawPacket = _dataBuffer.AsMemory(start, length);

        _header = new RtpHeader(_rawPacket.Span);
        _payload = _rawPacket[_header.Length..];
        IsReadyToUse = true;
    }

    public byte[] ReleaseBuffer()
    {
        if (!IsReadyToUse)
        {
            throw new Exception("RtpPacket has no data");
        }

        IsReadyToUse = false;
        var temp = _dataBuffer!;
        _dataBuffer = null;
        _rawPacket = null;
        _header = null;
        _payload = null;
        return temp;
    }

    public void ApplyPayload(ReadOnlySpan<byte> newPayload)
    {
        newPayload.CopyTo(_payload.Span);
    }

    public void ApplyHeaderChanges()
    {
        Header.WriteTo(_dataBuffer);
    }
}