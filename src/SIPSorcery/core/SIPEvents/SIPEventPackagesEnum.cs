namespace SIPSorcery.SIP
{
    public enum SIPEventPackagesEnum
    {
        None,

        /// <summary>
        /// RFC4235 "An INVITE-Initiated Dialog Event Package for the Session Initiation Protocol (SIP)".
        /// </summary>
        Dialog,

        /// <summary>
        /// RFC3842 "A Message Summary and Message Waiting Indication Event Package for the Session
        /// Initiation Protocol (SIP)"
        /// </summary>
        MessageSummary,

        /// <summary>
        /// RFC3856.
        /// </summary>
        Presence,

        /// <summary>
        /// RFC3515 "The Session Initiation Protocol (SIP) Refer Method".
        /// </summary>
        Refer
    }
}