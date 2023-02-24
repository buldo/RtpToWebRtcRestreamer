namespace SIPSorcery.SIP
{
    /// <summary>
    /// A list of the different SIP request methods that are supported.
    /// </summary>
    public enum SIPMethodsEnum
    {
        NONE = 0,
        UNKNOWN = 1,

        // Core.
        REGISTER = 2,
        INVITE = 3,
        BYE = 4,
        ACK = 5,
        CANCEL = 6,
        OPTIONS = 7,

        INFO = 8,           // RFC2976.
        NOTIFY = 9,         // RFC3265.
        SUBSCRIBE = 10,     // RFC3265.
        PUBLISH = 11,       // RFC3903.
        PING = 13,
        REFER = 14,         // RFC3515 "The Session Initiation Protocol (SIP) Refer Method"
        MESSAGE = 15,       // RFC3428.
        PRACK = 16,         // RFC3262.
        UPDATE = 17,        // RFC3311.
    }
}