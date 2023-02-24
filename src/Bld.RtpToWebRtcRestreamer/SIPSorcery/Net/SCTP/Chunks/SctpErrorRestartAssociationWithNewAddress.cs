using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    /// <summary>
    /// An INIT was received on an existing association.But the INIT added addresses to the
    /// association that were previously NOT part of the association. The new addresses are 
    /// listed in the error code.This ERROR is normally sent as part of an ABORT refusing the INIT.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.3.10.11
    /// </remarks>
    public struct SctpErrorRestartAssociationWithNewAddress : ISctpErrorCause
    {
        public SctpErrorCauseCode CauseCode => SctpErrorCauseCode.RestartAssociationWithNewAddress;

        /// <summary>
        /// Each New Address TLV is an exact copy of the TLV that was found
        /// in the INIT chunk that was new, including the Parameter Type and the
        /// Parameter Length.
        /// </summary>
        public byte[] NewAddressTLVs;

        public ushort GetErrorCauseLength(bool padded)
        {
            ushort len = (ushort)(4 + ((NewAddressTLVs != null) ? NewAddressTLVs.Length : 0));
            return padded ? SctpPadding.PadTo4ByteBoundary(len) : len;
        }

        public int WriteTo(byte[] buffer, int posn)
        {
            var len = GetErrorCauseLength(true);
            NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
            NetConvert.ToBuffer(len, buffer, posn + 2);
            if (NewAddressTLVs != null)
            {
                Buffer.BlockCopy(NewAddressTLVs, 0, buffer, posn + 4, NewAddressTLVs.Length);
            }
            return len;
        }
    }
}