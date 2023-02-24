using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SIPSorcery.Net;

using SIPSorceryMedia.Abstractions;

using WebSocketSharp.Server;
namespace Bld.RtpToWebRtcRestreamer;

internal class ClientConnectionsHandler : IClientConnectionsHandler
{
    private List<WebRTCWebSocketPeer> _peers = new();

    public Task ProcessNewClientAsync(string sdp)
    {
        return Task.CompletedTask;
    }
}