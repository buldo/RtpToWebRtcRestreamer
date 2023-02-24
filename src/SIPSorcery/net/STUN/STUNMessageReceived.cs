using System.Net;

namespace SIPSorcery.Net
{
    public delegate void STUNMessageReceived(IPEndPoint receivedEndPoint, IPEndPoint receivedOnEndPoint, byte[] buffer, int bufferLength);
}