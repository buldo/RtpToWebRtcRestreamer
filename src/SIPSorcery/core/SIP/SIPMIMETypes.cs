namespace SIPSorcery.SIP
{
    public static class SIPMIMETypes
    {
        public const string DIALOG_INFO_CONTENT_TYPE = "application/dialog-info+xml";   // RFC4235 INVITE dialog event package.
        public const string MWI_CONTENT_TYPE = "application/simple-message-summary";    // RFC3842 MWI event package.
        public const string REFER_CONTENT_TYPE = "message/sipfrag";                     // RFC3515 REFER event package.
        public const string MWI_TEXT_TYPE = "text/text";
        public const string PRESENCE_NOTIFY_CONTENT_TYPE = "application/pidf+xml";      // RFC3856 presence event package.
    }
}