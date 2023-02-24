using System;

namespace SIPSorcery.SIP
{
    public static class SIPProtocolsType
    {
        public static SIPProtocolsEnum GetProtocolType(string protocolType)
        {
            return (SIPProtocolsEnum)Enum.Parse(typeof(SIPProtocolsEnum), protocolType, true);
        }

        public static SIPProtocolsEnum GetProtocolTypeFromId(int protocolTypeId)
        {
            return (SIPProtocolsEnum)Enum.Parse(typeof(SIPProtocolsEnum), protocolTypeId.ToString(), true);
        }

        public static bool IsAllowedProtocol(string protocol)
        {
            try
            {
                Enum.Parse(typeof(SIPProtocolsEnum), protocol, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true for connectionless transport protocols, such as UDP, and false for
        /// connection oriented protocols.
        /// </summary>
        /// <param name="protocol">The protocol to check.</param>
        /// <returns>True if the protocol is connectionless.</returns>
        public static bool IsConnectionless(SIPProtocolsEnum protocol)
        {
            if (protocol == SIPProtocolsEnum.udp)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}