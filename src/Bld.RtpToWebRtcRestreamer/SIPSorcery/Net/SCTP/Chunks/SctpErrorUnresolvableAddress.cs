using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    /// <summary>
    /// Indicates that the sender is not able to resolve the specified address parameter
    /// (e.g., type of address is not supported by the sender).  This is usually sent in
    /// combination with or within an ABORT.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.3.10.5
    /// </remarks>
    public struct SctpErrorUnresolvableAddress : ISctpErrorCause
    {
        public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.UnresolvableAddress;

        /// <summary>
        /// The Unresolvable Address field contains the complete Type, Length,
        /// and Value of the address parameter(or Host Name parameter) that
        /// contains the unresolvable address or host name.
        /// </summary>
        public byte[] UnresolvableAddress;

        public ushort GetErrorCauseLength(bool padded)
        {
            ushort len = (ushort)(4 + ((UnresolvableAddress != null) ? UnresolvableAddress.Length : 0));
            return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
        }

        public int WriteTo(byte[] buffer, int posn)
        {
            var len = GetErrorCauseLength(true);
            NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
            NetConvert.ToBuffer(len, buffer, posn + 2);
            if (UnresolvableAddress != null)
            {
                Buffer.BlockCopy(UnresolvableAddress, 0, buffer, posn + 4, UnresolvableAddress.Length);
            }
            return len;
        }
    }
}