namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP;

public enum SctpAssociationState
{
    Closed,
    CookieWait,
    CookieEchoed,
    Established,
    ShutdownPending,
    ShutdownSent,
    //ShutdownReceived,
    ShutdownAckSent
}