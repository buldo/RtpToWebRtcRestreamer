//-----------------------------------------------------------------------------
// Filename: SIPDNSConstants.cs
//
// Description: Holds constant fields related to SIP DNS resolution.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 19 Jun 2010	Aaron Clauson	Created, Hobart, Australia.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace SIPSorcery.SIP
{
    public class SIPDNSConstants
    {
        public const string NAPTR_SIP_UDP_SERVICE = "SIP+D2U";
        public const string NAPTR_SIP_TCP_SERVICE = "SIP+D2T";
        public const string NAPTR_SIPS_TCP_SERVICE = "SIPS+D2T";
        public const string NAPTR_SIP_WEBSOCKET_SERVICE = "SIP+D2W";
        public const string NAPTR_SIPS_WEBSOCKET_SERVICE = "SIPS+D2W";

        public const string SRV_SIP_TCP_QUERY_PREFIX = "_sip._tcp.";
        public const string SRV_SIP_UDP_QUERY_PREFIX = "_sip._udp.";
        public const string SRV_SIP_TLS_QUERY_PREFIX = "_sip._tls.";
        public const string SRV_SIPS_TCP_QUERY_PREFIX = "_sips._tcp.";
        public const string SRV_SIP_WEBSOCKET_QUERY_PREFIX = "_sip._ws.";
        public const string SRV_SIPS_WEBSOCKET_QUERY_PREFIX = "_sips._ws.";
    }
}
