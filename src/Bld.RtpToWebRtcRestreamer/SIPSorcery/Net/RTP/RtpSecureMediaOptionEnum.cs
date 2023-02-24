namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    public enum RtpSecureMediaOptionEnum
    {
        /// <summary>
        /// Secure media not used.
        /// </summary>
        None,

        /// <summary>
        /// Secure media controled by DtlsSrtp for WebRTC.
        /// </summary>
        DtlsSrtp,

        /// <summary>
        /// Secure media negotiated with SDP crypto attributes.
        /// </summary>
        SdpCryptoNegotiation,
    }
}