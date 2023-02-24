namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    /// <summary>
    /// The actions required for unrecognised chunks. The byte value corresponds to the highest 
    /// order two bits of the chunk type value.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.2
    /// </remarks>
    public enum SctpUnrecognisedChunkActions : byte
    {
        /// <summary>
        /// Stop processing this SCTP packet and discard it, do not process any further chunks within it.
        /// </summary>
        Stop = 0x00,

        /// <summary>
        /// Stop processing this SCTP packet and discard it, do not process any further chunks within it, and report the
        /// unrecognized chunk in an 'Unrecognized Chunk Type'.
        /// </summary>
        StopAndReport = 0x01,

        /// <summary>
        /// Skip this chunk and continue processing.
        /// </summary>
        Skip = 0x02,

        /// <summary>
        /// Skip this chunk and continue processing, but report in an ERROR chunk using the 'Unrecognized Chunk Type' cause of
        /// error.
        /// </summary>
        SkipAndReport = 0x03
    }
}