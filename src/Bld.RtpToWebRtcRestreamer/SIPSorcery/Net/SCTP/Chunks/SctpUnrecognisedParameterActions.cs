namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    /// <summary>
    /// The actions required for unrecognised parameters. The byte value corresponds to the highest 
    /// order two bits of the parameter type value.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.2.1
    /// </remarks>
    public enum SctpUnrecognisedParameterActions : byte
    {
        /// <summary>
        /// Stop processing this parameter; do not process any further parameters within this chunk.
        /// </summary>
        Stop = 0x00,

        /// <summary>
        /// Stop processing this parameter, do not process any further parameters within this chunk, and report the unrecognized
        /// parameter in an 'Unrecognized Parameter'.
        /// </summary>
        StopAndReport = 0x01,

        /// <summary>
        /// Skip this parameter and continue processing.
        /// </summary>
        Skip = 0x02,

        /// <summary>
        /// Skip this parameter and continue processing but report the unrecognized parameter in an 'Unrecognized Parameter'.
        /// </summary>
        SkipAndReport = 0x03
    }
}