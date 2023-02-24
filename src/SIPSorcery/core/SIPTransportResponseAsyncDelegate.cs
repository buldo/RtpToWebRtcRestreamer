using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    public delegate Task SIPTransportResponseAsyncDelegate(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPResponse sipResponse);
}