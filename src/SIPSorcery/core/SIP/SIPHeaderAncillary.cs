namespace SIPSorcery.SIP
{
    public static class SIPHeaderAncillary
    {
        // Header parameters used in the core SIP protocol.
        public const string SIP_HEADERANC_TAG = "tag";
        public const string SIP_HEADERANC_BRANCH = "branch";
        public const string SIP_HEADERANC_RECEIVED = "received";
        public const string SIP_HEADERANC_TRANSPORT = "transport";
        public const string SIP_HEADERANC_MADDR = "maddr";

        // Via header parameter, documented in RFC 3581 "An Extension to the Session Initiation Protocol (SIP) 
        // for Symmetric Response Routing".
        public const string SIP_HEADERANC_RPORT = "rport";

        // SIP header parameter from RFC 3515 "The Session Initiation Protocol (SIP) Refer Method".
        public const string SIP_REFER_REPLACES = "Replaces";
    }
}