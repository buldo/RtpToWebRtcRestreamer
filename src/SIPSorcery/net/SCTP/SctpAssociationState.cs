namespace SIPSorcery.Net
{
    public enum SctpAssociationState
    {
        Closed,
        CookieWait,
        CookieEchoed,
        Established,
        ShutdownPending,
        ShutdownSent,
        ShutdownReceived,
        ShutdownAckSent
    }
}