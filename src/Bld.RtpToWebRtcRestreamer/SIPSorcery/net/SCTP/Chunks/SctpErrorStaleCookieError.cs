using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// Indicates the receipt of a valid State Cookie that has expired.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.3.10.3
    /// </remarks>
    public struct SctpErrorStaleCookieError : ISctpErrorCause
    {
        private const ushort ERROR_CAUSE_LENGTH = 8;

        public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.StaleCookieError;

        /// <summary>
        /// The difference, in microseconds, between the current time and the time the State Cookie expired.
        /// </summary>
        public uint MeasureOfStaleness;

        public ushort GetErrorCauseLength(bool padded) => ERROR_CAUSE_LENGTH;

        public int WriteTo(byte[] buffer, int posn)
        {
            NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
            NetConvert.ToBuffer(ERROR_CAUSE_LENGTH, buffer, posn + 2);
            NetConvert.ToBuffer(MeasureOfStaleness, buffer, posn + 4);
            return ERROR_CAUSE_LENGTH;
        }
    }
}