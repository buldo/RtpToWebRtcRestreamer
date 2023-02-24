using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks
{
    /// <summary>
    /// The values of the Chunk Types.
    /// </summary>
    /// <remarks>
    /// https://tools.ietf.org/html/rfc4960#section-3.2
    /// </remarks>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum SctpChunkType : byte
    {
        DATA = 0,
        INIT = 1,
        INIT_ACK = 2,
        SACK = 3,
        HEARTBEAT = 4,
        HEARTBEAT_ACK = 5,
        ABORT = 6,
        SHUTDOWN = 7,
        SHUTDOWN_ACK = 8,
        ERROR = 9,
        COOKIE_ECHO = 10,
        COOKIE_ACK = 11,
        ECNE = 12,          // Not used (specified in the RFC for future use).
        CWR = 13,           // Not used (specified in the RFC for future use).
        SHUTDOWN_COMPLETE = 14,

        // Not defined in RFC4960.
        //AUTH = 15,
        //PKTDROP = 129,
        //RE_CONFIG = 130,
        //FORWARDTSN = 192,
        //ASCONF = 193,
        //ASCONF_ACK = 128,
    }
}