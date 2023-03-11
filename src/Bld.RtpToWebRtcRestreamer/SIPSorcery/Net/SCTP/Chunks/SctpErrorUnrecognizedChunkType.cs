using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;

/// <summary>
/// Indicates that the sender is out of resource.  This
/// is usually sent in combination with or within an ABORT.
/// </summary>
/// <remarks>
/// https://tools.ietf.org/html/rfc4960#section-3.3.10.6
/// </remarks>
internal struct SctpErrorUnrecognizedChunkType : ISctpErrorCause
{
    public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.UnrecognizedChunkType;

    /// <summary>
    /// The Unrecognized Chunk field contains the unrecognized chunk from
    /// the SCTP packet complete with Chunk Type, Chunk Flags, and Chunk
    /// Length.
    /// </summary>
    public byte[] UnrecognizedChunk;

    public ushort GetErrorCauseLength(bool padded)
    {
        var len = (ushort)(4 + (UnrecognizedChunk != null ? UnrecognizedChunk.Length : 0));
        return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
    }

    public int WriteTo(byte[] buffer, int posn)
    {
        var len = GetErrorCauseLength(true);
        NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
        NetConvert.ToBuffer(len, buffer, posn + 2);
        if (UnrecognizedChunk != null)
        {
            Buffer.BlockCopy(UnrecognizedChunk, 0, buffer, posn + 4, UnrecognizedChunk.Length);
        }
        return len;
    }
}