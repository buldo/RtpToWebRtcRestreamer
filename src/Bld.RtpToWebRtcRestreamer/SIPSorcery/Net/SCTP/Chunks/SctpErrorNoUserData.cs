using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    /// <summary>
    /// This error cause is returned to the originator of a
    /// DATA chunk if a received DATA chunk has no user data.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.3.10.9
    /// </remarks>
    internal struct SctpErrorNoUserData : ISctpErrorCause
    {
        private const ushort ERROR_CAUSE_LENGTH = 8;

        public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.NoUserData;

        /// <summary>
        /// The TSN value field contains the TSN of the DATA chunk received
        /// with no user data field.
        /// </summary>
        public uint TSN;

        public ushort GetErrorCauseLength(bool padded) => ERROR_CAUSE_LENGTH;

        public int WriteTo(byte[] buffer, int posn)
        {
            NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
            NetConvert.ToBuffer(ERROR_CAUSE_LENGTH, buffer, posn + 2);
            NetConvert.ToBuffer(TSN, buffer, posn + 4);
            return ERROR_CAUSE_LENGTH;
        }
    }
}