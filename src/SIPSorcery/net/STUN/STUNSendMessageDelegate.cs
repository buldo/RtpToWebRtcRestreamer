using System.Net;

namespace SIPSorcery.Net
{
    public delegate void STUNSendMessageDelegate(IPEndPoint dst, byte[] buffer);
}