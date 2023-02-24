namespace SIPSorcery.SIP
{
    public static class SIPTimings
    {
        /// <summary>
        /// Value of the SIP defined timer T1 in milliseconds and is the time for the first retransmit.
        /// Should not need to be adjusted in normal circumstances.
        /// </summary>
        public static int T1 = 500;

        /// <summary>
        /// Value of the SIP defined timer T2 in milliseconds and is the maximum time between retransmits.
        /// Should not need to be adjusted in normal circumstances.
        /// </summary>
        public static int T2 = 4000;

        /// <summary>
        /// Value of the SIP defined timer T6 in milliseconds and is the period after which a transaction 
        /// has timed out. Should not need to be adjusted in normal circumstances.
        /// </summary>
        public static int T6 = 64 * T1;

        /// <summary>
        /// The number of milliseconds a transaction can stay in the proceeding state 
        /// (i.e. an INVITE will ring for) before the call is given up and timed out.     
        /// </summary>
        public static int MAX_RING_TIME = 180000;
    }
}