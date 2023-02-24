using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    public delegate Task SIPTransportRequestAsyncDelegate(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest);
}