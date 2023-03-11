using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;

/// <summary>
/// This error cause is returned to the originator of the INIT ACK chunk 
/// if the receiver does not recognize one or more optional variable parameters in 
/// the INIT ACK chunk.
/// </summary>
/// <remarks>
/// https://tools.ietf.org/html/rfc4960#section-3.3.10.8
/// </remarks>
internal struct SctpErrorUnrecognizedParameters : ISctpErrorCause
{
    public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.UnrecognizedParameters;

    /// <summary>
    /// The Unrecognized Parameters field contains the unrecognized
    /// parameters copied from the INIT ACK chunk complete with TLV. This
    /// error cause is normally contained in an ERROR chunk bundled with
    /// the COOKIE ECHO chunk when responding to the INIT ACK, when the
    /// sender of the COOKIE ECHO chunk wishes to report unrecognized
    /// parameters.
    /// </summary>
    public byte[] UnrecognizedParameters;

    public ushort GetErrorCauseLength(bool padded)
    {
        var len = (ushort)(4 + (UnrecognizedParameters != null ? UnrecognizedParameters.Length : 0));
        return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
    }

    public int WriteTo(byte[] buffer, int posn)
    {
        var len = GetErrorCauseLength(true);
        NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
        NetConvert.ToBuffer(len, buffer, posn + 2);
        if (UnrecognizedParameters != null)
        {
            Buffer.BlockCopy(UnrecognizedParameters, 0, buffer, posn + 4, UnrecognizedParameters.Length);
        }
        return len;
    }
}