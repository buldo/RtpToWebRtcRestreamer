using System;
using System.Runtime.CompilerServices;

namespace SIPSorcery.SIP
{
    public class SIPAuthenticationHeader
    {
        public SIPAuthorisationDigest SIPDigest;
        public string Value;
        public SIPAuthorisationHeadersEnum AuthorisationType;

        private SIPAuthenticationHeader() : this(new SIPAuthorisationDigest())
        {
        }

        public SIPAuthenticationHeader(SIPAuthorisationDigest sipDigest)
        {
            SIPDigest = sipDigest;
            Value = string.Empty;
            AuthorisationType = sipDigest?.AuthorisationType ?? SIPAuthorisationHeadersEnum.Authorize;
        }

        public SIPAuthenticationHeader(SIPAuthorisationHeadersEnum authorisationType, string realm, string nonce)
        {
            SIPDigest = new SIPAuthorisationDigest(authorisationType)
            {
                Realm = realm,
                Nonce = nonce
            };
            Value = string.Empty;
            AuthorisationType = authorisationType;
        }

        public static SIPAuthenticationHeader ParseSIPAuthenticationHeader(SIPAuthorisationHeadersEnum authorizationType, string headerValue)
        {
            try
            {
                var authHeader = new SIPAuthenticationHeader
                {
                    Value = headerValue
                };
                if (headerValue.StartsWith(SIPAuthorisationDigest.METHOD))
                {
                    authHeader.SIPDigest = SIPAuthorisationDigest.ParseAuthorisationDigest(authorizationType, headerValue);
                }
                else
                {
                    authHeader.SIPDigest = new SIPAuthorisationDigest(SIPAuthorisationHeadersEnum.Unknown);
                }
                authHeader.AuthorisationType = authHeader.SIPDigest.AuthorisationType;
                return authHeader;
            }
            catch
            {
                throw new ApplicationException("Error parsing SIP authentication header request, " + headerValue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string BuildAuthorisationHeaderName(SIPAuthorisationHeadersEnum authorisationHeaderType)
        {
            string authHeader = null;
            if (authorisationHeaderType == SIPAuthorisationHeadersEnum.Authorize)
            {
                authHeader = SIPHeaders.SIP_HEADER_AUTHORIZATION + ": ";
            }
            else if (authorisationHeaderType == SIPAuthorisationHeadersEnum.ProxyAuthenticate)
            {
                authHeader = SIPHeaders.SIP_HEADER_PROXYAUTHENTICATION + ": ";
            }
            else if (authorisationHeaderType == SIPAuthorisationHeadersEnum.ProxyAuthorization)
            {
                authHeader = SIPHeaders.SIP_HEADER_PROXYAUTHORIZATION + ": ";
            }
            else if (authorisationHeaderType == SIPAuthorisationHeadersEnum.WWWAuthenticate)
            {
                authHeader = SIPHeaders.SIP_HEADER_WWWAUTHENTICATE + ": ";
            }
            else
            {
                authHeader = SIPHeaders.SIP_HEADER_AUTHORIZATION + ": ";
            }

            return authHeader;
        }

        public override string ToString()
        {
            if (SIPDigest != null)
            {
                var authorisationHeaderType = (SIPDigest.AuthorisationResponseType != SIPAuthorisationHeadersEnum.Unknown) ? SIPDigest.AuthorisationResponseType : SIPDigest.AuthorisationType;
                string authHeader = BuildAuthorisationHeaderName(authorisationHeaderType);
                return authHeader + SIPDigest.ToString();
            }
            else if (!string.IsNullOrEmpty(Value))
            {
                string authHeader = BuildAuthorisationHeaderName(AuthorisationType);
                return authHeader + Value;
            }
            else
            {
                return null;
            }
        }
    }
}