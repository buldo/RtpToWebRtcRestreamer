using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;

/// <summary>
/// Indicates that one or more mandatory Type-Length-Value (TLV) format
/// parameters are missing in a received INIT or INIT ACK.
/// </summary>
/// <remarks>
/// https://tools.ietf.org/html/rfc4960#section-3.3.10.2
/// </remarks>
internal struct SctpErrorMissingMandatoryParameter : ISctpErrorCause
{
    public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.MissingMandatoryParameter;

    public List<ushort> MissingParameters;

    public ushort GetErrorCauseLength(bool padded)
    {
        var len = (ushort)(4 + (MissingParameters != null ? MissingParameters.Count * 2 : 0));
        return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
    }

    public int WriteTo(byte[] buffer, int posn)
    {
        var len = GetErrorCauseLength(true);
        NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
        NetConvert.ToBuffer(len, buffer, posn + 2);
        if (MissingParameters != null)
        {
            var valPosn = posn + 4;
            foreach (var missing in MissingParameters)
            {
                NetConvert.ToBuffer(missing, buffer, valPosn);
                valPosn += 2;
            }
        }
        return len;
    }
}