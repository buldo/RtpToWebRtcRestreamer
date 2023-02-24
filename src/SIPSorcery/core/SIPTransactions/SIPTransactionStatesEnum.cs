namespace SIPSorcery.SIP
{
    public enum SIPTransactionStatesEnum
    {
        Calling = 1,
        Completed = 2,
        Confirmed = 3,
        Proceeding = 4,
        Terminated = 5,
        Trying = 6,

        /// <summary>
        /// This state is not in the SIP RFC but is deemed the most practical 
        /// way to record that an INVITE has been cancelled. Other states 
        /// will have ramifications for the transaction lifetime.
        /// </summary>
        Cancelled = 7
    }
}