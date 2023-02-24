namespace SIPSorcery.SIP
{
    public enum SIPTransactionTypesEnum
    {
        /// <summary>
        /// User agent server transaction.
        /// </summary>
        InviteServer = 1,

        /// <summary>
        /// All non-INVITE transaction types.
        /// </summary>
        NonInvite = 2,

        /// <summary>
        /// User agent client transaction.
        /// </summary>
        InviteClient = 3,
    }
}