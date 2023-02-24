using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    public delegate Task SIPMessageReceivedAsyncDelegate(SIPChannel sipChannel, SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, byte[] buffer);
}