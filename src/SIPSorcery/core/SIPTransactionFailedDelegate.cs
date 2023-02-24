using System.Net.Sockets;

namespace SIPSorcery.SIP
{
    public delegate void SIPTransactionFailedDelegate(SIPTransaction sipTransaction, SocketError failureReason);
}