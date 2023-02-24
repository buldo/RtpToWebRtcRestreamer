namespace SIPSorcery.Net
{
    public interface ISctpErrorCause
    {
        SctpErrorCauseCode CauseCode { get; }
        ushort GetErrorCauseLength(bool padded);
        int WriteTo(byte[] buffer, int posn);
    }
}