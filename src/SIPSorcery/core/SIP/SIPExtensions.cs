namespace SIPSorcery.SIP
{
    ///<summary>
    /// List of SIP extensions to RFC3262.
    /// </summary> 
    public enum SIPExtensions
    {
        None = 0,
        Prack = 1,          // Reliable provisional responses as per RFC3262.
        NoReferSub = 2,     // No subscription for REFERs as per RFC4488.
        Replaces = 3,
        SipRec = 4,
        MultipleRefer = 5,
    }
}