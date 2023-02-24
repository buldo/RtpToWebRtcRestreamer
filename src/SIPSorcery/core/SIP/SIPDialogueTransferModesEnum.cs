namespace SIPSorcery.SIP
{
    public enum SIPDialogueTransferModesEnum
    {
        Default = 0,
        PassThru = 1,           // REFER requests will be treated as an in-dialogue request and passed through to user agents.
        NotAllowed = 2,         // REFER requests will be blocked.
        BlindPlaceCall = 3,     // REFER requests without a replaces parameter will initiate a new call.
    }
}