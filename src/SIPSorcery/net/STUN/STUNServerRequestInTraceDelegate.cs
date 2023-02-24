using System.Net;

namespace SIPSorcery.Net
{
    public delegate void STUNServerRequestInTraceDelegate(IPEndPoint localEndPoint, IPEndPoint fromEndPoint, STUNMessage stunMessage);
}