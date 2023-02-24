//-----------------------------------------------------------------------------
// Filename: SIPConstants.cs
//
// Description: SIP constants.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 17 Sep 2005	Aaron Clauson	Created, Hobart, Australia.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Text;

// ReSharper disable InconsistentNaming

namespace SIPSorcery.SIP
{
    public static class SIPConstants
    {
        public const string CRLF = "\r\n";

        public const string SIP_VERSION_STRING = "SIP";
        public const int SIP_MAJOR_VERSION = 2;
        public const int SIP_MINOR_VERSION = 0;
        public const string SIP_FULLVERSION_STRING = "SIP/2.0";

        public const int NONCE_TIMEOUT_MINUTES = 5;                         // Length of time an issued nonce is valid for.

        /// <summary>
        /// The maximum size supported for an incoming SIP message.
        /// </summary>
        /// <remarks>
        /// From https://tools.ietf.org/html/rfc3261#section-18.1.1:
        /// However, implementations MUST be able to handle messages up to the maximum
        /// datagram packet size.For UDP, this size is 65,535 bytes, including
        /// IP and UDP headers.
        /// </remarks>
        public const int SIP_MAXIMUM_RECEIVE_LENGTH = 65535;

        public const string SIP_REQUEST_REGEX = @"^\w+ .* SIP/.*";          // bnf:	Request-Line = Method SP Request-URI SP SIP-Version CRLF
        public const string SIP_RESPONSE_REGEX = @"^SIP/.* \d{3}";          // bnf: Status-Line = SIP-Version SP Status-Code SP Reason-Phrase CRLF
        public const string SIP_BRANCH_MAGICCOOKIE = "z9hG4bK";
        public const string SIP_DEFAULT_USERNAME = "Anonymous";
        public const string SIP_DEFAULT_FROMURI = "sip:thisis@anonymous.invalid";
        public const string SIP_REGISTER_REMOVEALL = "*";                   // The value in a REGISTER request id a UA wishes to remove all REGISTER bindings.
        public const string SIP_LOOSEROUTER_PARAMETER = "lr";
        public const string SIP_REMOTEHANGUP_CAUSE = "remote end hungup";
        public const char HEADER_DELIMITER_CHAR = ':';

        public const int DEFAULT_MAX_FORWARDS = 70;
        public const int DEFAULT_REGISTEREXPIRY_SECONDS = 600;
        public const ushort DEFAULT_SIP_PORT = 5060;
        public const ushort DEFAULT_SIP_TLS_PORT = 5061;
        public const ushort DEFAULT_SIP_WEBSOCKET_PORT = 80;
        public const ushort DEFAULT_SIPS_WEBSOCKET_PORT = 443;

        public const string ALLOWED_SIP_METHODS = "ACK, BYE, CANCEL, INFO, INVITE, NOTIFY, OPTIONS, PRACK, REFER, REGISTER, SUBSCRIBE";

        private static string _userAgentVersion;
        public static string SipUserAgentVersionString
        {
            get
            {
                if (_userAgentVersion == null)
                {
                    _userAgentVersion = $"sipsorcery_v{Assembly.GetExecutingAssembly().GetName().Version}";
                }

                return _userAgentVersion;
            }
            set
            {
                _userAgentVersion = value;
            }
        }

        public static Encoding DEFAULT_ENCODING = Encoding.UTF8;

        /// <summary>
        /// Gets the default SIP port for the protocol. 
        /// </summary>
        /// <param name="protocol">The transport layer protocol to get the port for.</param>
        /// <returns>The default port to use.</returns>
        public static int GetDefaultPort(SIPProtocolsEnum protocol)
        {
            switch (protocol)
            {
                case SIPProtocolsEnum.udp:
                    return SIPConstants.DEFAULT_SIP_PORT;
                case SIPProtocolsEnum.tcp:
                    return SIPConstants.DEFAULT_SIP_PORT;
                case SIPProtocolsEnum.tls:
                    return SIPConstants.DEFAULT_SIP_TLS_PORT;
                case SIPProtocolsEnum.ws:
                    return SIPConstants.DEFAULT_SIP_WEBSOCKET_PORT;
                case SIPProtocolsEnum.wss:
                    return SIPConstants.DEFAULT_SIPS_WEBSOCKET_PORT;
                default:
                    throw new ApplicationException($"Protocol {protocol} was not recognised in GetDefaultPort.");
            }
        }
    }
}