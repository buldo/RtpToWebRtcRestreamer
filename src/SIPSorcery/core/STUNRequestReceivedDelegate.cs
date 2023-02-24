using System.Net;

namespace SIPSorcery.SIP
{
    public delegate void STUNRequestReceivedDelegate(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, byte[] buffer, int bufferLength);
}