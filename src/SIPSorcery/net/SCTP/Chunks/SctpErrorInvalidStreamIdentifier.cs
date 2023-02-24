using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// Invalid Stream Identifier: Indicates endpoint received a DATA chunk
    /// sent to a nonexistent stream.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.3.10.1
    /// </remarks>
    public struct SctpErrorInvalidStreamIdentifier : ISctpErrorCause
    {
        private const ushort ERROR_CAUSE_LENGTH = 8;

        public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.InvalidStreamIdentifier;

        /// <summary>
        /// The invalid stream identifier.
        /// </summary>
        public ushort StreamID;

        public ushort GetErrorCauseLength(bool padded) => ERROR_CAUSE_LENGTH;

        public int WriteTo(byte[] buffer, int posn)
        {
            NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
            NetConvert.ToBuffer(ERROR_CAUSE_LENGTH, buffer, posn + 2);
            NetConvert.ToBuffer(StreamID, buffer, posn + 4);
            return ERROR_CAUSE_LENGTH;
        }
    }
}