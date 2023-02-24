using System.Threading;
using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    public delegate Task<SIPEndPoint> ResolveSIPUriDelegateAsync(SIPURI uri, bool preferIPv6, CancellationToken ct);
}