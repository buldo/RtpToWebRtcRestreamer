using System.Net.Sockets;
using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    public delegate Task<SocketError> SIPTransactionResponseReceivedDelegate(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPTransaction sipTransaction, SIPResponse sipResponse);
}