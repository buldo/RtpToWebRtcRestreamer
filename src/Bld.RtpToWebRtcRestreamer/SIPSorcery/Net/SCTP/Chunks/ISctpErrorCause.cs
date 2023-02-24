namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    public interface ISctpErrorCause
    {
        SctpErrorCauseCode CauseCode { get; }
        ushort GetErrorCauseLength(bool padded);
        int WriteTo(byte[] buffer, int posn);
    }
}