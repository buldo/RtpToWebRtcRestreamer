using System.Net;

namespace SIPSorcery.Net
{
    public delegate void STUNServerResponseOutTraceDelegate(IPEndPoint localEndPoint, IPEndPoint toEndPoint, STUNMessage stunMessage);
}