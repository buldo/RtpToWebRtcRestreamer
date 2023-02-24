using System;
using System.Text;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// This error cause MAY be included in ABORT chunks that are sent
    /// because of an upper-layer request.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.3.10.12
    /// </remarks>
    public struct SctpErrorUserInitiatedAbort : ISctpErrorCause
    {
        public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.UserInitiatedAbort;

        /// <summary>
        /// Optional descriptive abort reason from Upper Layer Protocol (ULP).
        /// </summary>
        public string AbortReason;

        public ushort GetErrorCauseLength(bool padded)
        {
            ushort len = (ushort)(4 + ((!string.IsNullOrEmpty(AbortReason)) ? Encoding.UTF8.GetByteCount(AbortReason) : 0));
            return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
        }

        public int WriteTo(byte[] buffer, int posn)
        {
            var len = GetErrorCauseLength(true);
            NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
            NetConvert.ToBuffer(len, buffer, posn + 2);
            if (!string.IsNullOrEmpty(AbortReason))
            {
                var reasonBuffer = Encoding.UTF8.GetBytes(AbortReason);
                Buffer.BlockCopy(reasonBuffer, 0, buffer, posn + 4, reasonBuffer.Length);
            }
            return len;
        }
    }
}