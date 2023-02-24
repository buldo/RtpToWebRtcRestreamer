namespace SIPSorcery.SIP
{
    public class SIPServices
    {
        public static SIPServicesEnum GetService(string service)
        {
            if (service == SIPDNSConstants.NAPTR_SIP_UDP_SERVICE)
            {
                return SIPServicesEnum.sipudp;
            }
            else if (service == SIPDNSConstants.NAPTR_SIP_TCP_SERVICE)
            {
                return SIPServicesEnum.siptcp;
            }
            else if (service == SIPDNSConstants.NAPTR_SIPS_TCP_SERVICE)
            {
                return SIPServicesEnum.sipstcp;
            }
            else
            {
                return SIPServicesEnum.none;
            }
        }

        /// <summary>
        /// This method is needed because "sips" URI's have to be looked
        /// up with a SRV record containing "tcp" NOT "tls" and same for web sockets.
        /// </summary>
        /// <param name="uri">The SIP URI to determine the SRV record protocol for.</param>
        /// <returns>The protocol to use in a SRV record lookup.</returns>
        public static SIPProtocolsEnum GetSRVProtocolForSIPURI(SIPURI uri)
        {
            if (uri.Scheme == SIPSchemesEnum.sips)
            {
                if (uri.Protocol == SIPProtocolsEnum.wss)
                {
                    return SIPProtocolsEnum.ws;
                }
                else
                {
                    return SIPProtocolsEnum.tcp;
                }
            }
            else
            {
                return uri.Protocol;
            }
        }
    }
}