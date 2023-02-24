namespace SIPSorcery.SIP
{
    public delegate void SIPTransactionRequestRetransmitDelegate(SIPTransaction sipTransaction, SIPRequest sipRequest, int retransmitNumber);
}