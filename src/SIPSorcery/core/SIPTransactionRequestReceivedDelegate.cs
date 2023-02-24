using System.Net.Sockets;
using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    public delegate Task<SocketError> SIPTransactionRequestReceivedDelegate(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPTransaction sipTransaction, SIPRequest sipRequest);
}