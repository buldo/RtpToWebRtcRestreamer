namespace SIPSorcery.Net
{
    /// <remarks>
    /// Defined in https://tools.ietf.org/html/rfc4960#section-3.3.10
    /// </remarks>
    public enum SctpErrorCauseCode : ushort
    {
        InvalidStreamIdentifier = 1,
        MissingMandatoryParameter = 2,
        StaleCookieError = 3,
        OutOfResource = 4,
        UnresolvableAddress = 5,
        UnrecognizedChunkType = 6,
        InvalidMandatoryParameter = 7,
        UnrecognizedParameters = 8,
        NoUserData = 9,
        CookieReceivedWhileShuttingDown = 10,
        RestartAssociationWithNewAddress = 11,
        UserInitiatedAbort = 12,
        ProtocolViolation = 13
    }
}