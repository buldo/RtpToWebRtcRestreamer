namespace SIPSorcery.SIP
{
    public delegate void SIPTransactionResponseRetransmitDelegate(SIPTransaction sipTransaction, SIPResponse sipResponse, int retransmitNumber);
}