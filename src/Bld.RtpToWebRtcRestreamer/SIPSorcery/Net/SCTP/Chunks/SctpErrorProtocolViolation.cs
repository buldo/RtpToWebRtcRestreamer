using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;

/// <summary>
/// This error cause MAY be included in ABORT chunks that are sent
/// because an SCTP endpoint detects a protocol violation of the peer
/// that is not covered by any of the more specific error causes
/// </summary>
/// <remarks>
/// https://tools.ietf.org/html/rfc4960#section-3.3.10.13
/// </remarks>
internal struct SctpErrorProtocolViolation : ISctpErrorCause
{
    public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.ProtocolViolation;

    /// <summary>
    /// Optional description of the violation.
    /// </summary>
    public string AdditionalInformation;

    public ushort GetErrorCauseLength(bool padded)
    {
        var len = (ushort)(4 + (!string.IsNullOrEmpty(AdditionalInformation) ? Encoding.UTF8.GetByteCount(AdditionalInformation) : 0));
        return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
    }

    public int WriteTo(byte[] buffer, int posn)
    {
        var len = GetErrorCauseLength(true);
        NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
        NetConvert.ToBuffer(len, buffer, posn + 2);
        if (!string.IsNullOrEmpty(AdditionalInformation))
        {
            var reasonBuffer = Encoding.UTF8.GetBytes(AdditionalInformation);
            Buffer.BlockCopy(reasonBuffer, 0, buffer, posn + 4, reasonBuffer.Length);
        }
        return len;
    }
}