//-----------------------------------------------------------------------------
// Filename: SIPAuthorisationDigest.cs
//
// Description: Implements Digest Authentication as defined in RFC2617.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com
// 
// History:
// 08 Sep 2005	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    public class SIPAuthorisationDigest
    {
        public const string METHOD = "Digest";
        public const string QOP_AUTHENTICATION_VALUE = "auth";
        private const int NONCE_DEFAULT_COUNT = 1;
        private const string SHA256_ALGORITHM_ID = "SHA-256";

        private static ILogger logger = Log.Logger;

        private static char[] m_headerFieldRemoveChars = new char[] { ' ', '"', '\'' };

        public SIPAuthorisationHeadersEnum AuthorisationType { get; private set; } // This is the type of authorisation request received.
        public SIPAuthorisationHeadersEnum AuthorisationResponseType { get; private set; }      // If this is set it's the type of authorisation response to use otherwise use the same as the request (God knows why you need a different response header?!?)

        public string Realm;
        public string Username;
        public string Password;
        //public string DestinationURL;
        public string URI;
        public string Nonce;
        public string RequestType;
        public string Response;
        public string HA1;           // HTTP digest HA1. Contains the username, relam and password already hashed.

        public string Cnonce;        // Client nonce (used with WWW-Authenticate and qop=auth).
        public string Qop;           // Quality of Protection. Values permitted are auth (authentication) and auth-int (authentication with integrity protection).
        private int NonceCount = 0;  // Client nonce count.
        public string Opaque;

        public DigestAlgorithmsEnum DigestAlgorithm = DigestAlgorithmsEnum.MD5;

        public SIPAuthorisationDigest(DigestAlgorithmsEnum hashAlgorithm = DigestAlgorithmsEnum.MD5)
        {
            AuthorisationType = SIPAuthorisationHeadersEnum.ProxyAuthorization;
            DigestAlgorithm = hashAlgorithm;
        }

        public SIPAuthorisationDigest(SIPAuthorisationHeadersEnum authorisationType, DigestAlgorithmsEnum hashAlgorithm = DigestAlgorithmsEnum.MD5)
        {
            AuthorisationType = authorisationType;
            DigestAlgorithm = hashAlgorithm;
        }

        public static SIPAuthorisationDigest ParseAuthorisationDigest(SIPAuthorisationHeadersEnum authorisationType, string authorisationRequest)
        {
            SIPAuthorisationDigest authRequest = new SIPAuthorisationDigest(authorisationType);

            string noDigestHeader = Regex.Replace(authorisationRequest, $@"^\s*{METHOD}\s*", "", RegexOptions.IgnoreCase);
            string[] headerFields = noDigestHeader.Split(',');

            if (headerFields != null && headerFields.Length > 0)
            {
                foreach (string headerField in headerFields)
                {
                    int equalsIndex = headerField.IndexOf('=');

                    if (equalsIndex != -1 && equalsIndex < headerField.Length)
                    {
                        string headerName = headerField.Substring(0, equalsIndex).Trim();
                        string headerValue = headerField.Substring(equalsIndex + 1).Trim(m_headerFieldRemoveChars);

                        if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_REALM_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Realm = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_NONCE_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Nonce = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_USERNAME_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Username = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_RESPONSE_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Response = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_URI_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.URI = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_CNONCE_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Cnonce = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_NONCECOUNT_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            Int32.TryParse(headerValue, out authRequest.NonceCount);
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_QOP_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Qop = headerValue.ToLower();
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_OPAQUE_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            authRequest.Opaque = headerValue;
                        }
                        else if (Regex.Match(headerName, "^" + AuthHeaders.AUTH_ALGORITHM_KEY + "$", RegexOptions.IgnoreCase).Success)
                        {
                            //authRequest.Algorithhm = headerValue;

                            if (Enum.TryParse<DigestAlgorithmsEnum>(headerValue.Replace("-",""), true, out var alg))
                            {
                                authRequest.DigestAlgorithm = alg;
                            }
                            else
                            {
                                logger.LogWarning($"SIPAuthorisationDigest did not recognised digest algorithm value of {headerValue}, defaulting to {DigestAlgorithmsEnum.MD5}.");
                                authRequest.DigestAlgorithm = DigestAlgorithmsEnum.MD5;
                            }
                        }
                    }
                }
            }

            return authRequest;
        }

        public SIPAuthorisationDigest(
            SIPAuthorisationHeadersEnum authorisationType,
            string realm,
            string username,
            string password,
            string uri,
            string nonce,
            string request,
            DigestAlgorithmsEnum hashAlgorithm = DigestAlgorithmsEnum.MD5)
        {
            AuthorisationType = authorisationType;
            Realm = realm;
            Username = username;
            Password = password;
            URI = uri;
            Nonce = nonce;
            RequestType = request;

            DigestAlgorithm = hashAlgorithm;
        }

        public void SetCredentials(string username, string password, string uri, string method)
        {
            Username = username;
            Password = password;
            URI = uri;
            RequestType = method;
        }

        public void SetCredentials(string ha1, string uri, string method)
        {
            HA1 = ha1;
            URI = uri;
            RequestType = method;
        }

        public string GetDigest()
        {
            // Just to make things difficult For some authorisation requests the header changes when the authenticated response is generated.
            if (AuthorisationType == SIPAuthorisationHeadersEnum.ProxyAuthenticate)
            {
                AuthorisationResponseType = SIPAuthorisationHeadersEnum.ProxyAuthorization;
            }
            else if (AuthorisationType == SIPAuthorisationHeadersEnum.WWWAuthenticate)
            {
                AuthorisationResponseType = SIPAuthorisationHeadersEnum.Authorize;
            }

            // If the authorisation header has specified quality of protection equal to "auth" a client nonce needs to be supplied.
            string nonceCountStr = null;
            if (Qop == QOP_AUTHENTICATION_VALUE)
            {
                NonceCount = (NonceCount != 0) ? NonceCount : NONCE_DEFAULT_COUNT;
                nonceCountStr = GetPaddedNonceCount(NonceCount);

                if (Cnonce == null || Cnonce.Trim().Length == 0)
                {
                    Cnonce = Crypto.GetRandomInt().ToString();
                }
            }

            if (Nonce == null)
            {
                Nonce = Crypto.GetRandomString(12);
            }

            if (Password != null)
            {
                return HTTPDigest.DigestCalcResponse(
                    Username,
                    Realm,
                    Password,
                    URI,
                    Nonce,
                    nonceCountStr,
                    Cnonce,
                    Qop,
                    RequestType,
                    DigestAlgorithm);
            }
            else if (HA1 != null)
            {
                return HTTPDigest.DigestCalcResponse(
                    HA1,
                   URI,
                   Nonce,
                   nonceCountStr,
                   Cnonce,
                   Qop,
                   RequestType,
                   DigestAlgorithm);
            }
            else
            {
                throw new ApplicationException("SIP authorisation digest cannot be calculated. No password or HA1 available.");
            }
        }

        public override string ToString()
        {
            string authHeader = AuthHeaders.AUTH_DIGEST_KEY + " ";

            authHeader += (Username != null && Username.Trim().Length != 0) ? AuthHeaders.AUTH_USERNAME_KEY + "=\"" + Username + "\"" : null;
            authHeader += (authHeader.IndexOf('=') != -1) ? "," + AuthHeaders.AUTH_REALM_KEY + "=\"" + Realm + "\"" : AuthHeaders.AUTH_REALM_KEY + "=\"" + Realm + "\"";
            authHeader += (Nonce != null) ? "," + AuthHeaders.AUTH_NONCE_KEY + "=\"" + Nonce + "\"" : null;
            authHeader += (URI != null && URI.Trim().Length != 0) ? "," + AuthHeaders.AUTH_URI_KEY + "=\"" + URI + "\"" : null;
            authHeader += (Response != null && Response.Length != 0) ? "," + AuthHeaders.AUTH_RESPONSE_KEY + "=\"" + Response + "\"" : null;
            authHeader += (Cnonce != null) ? "," + AuthHeaders.AUTH_CNONCE_KEY + "=\"" + Cnonce + "\"" : null;
            authHeader += (NonceCount != 0) ? "," + AuthHeaders.AUTH_NONCECOUNT_KEY + "=" + GetPaddedNonceCount(NonceCount) : null;
            authHeader += (Qop != null) ? "," + AuthHeaders.AUTH_QOP_KEY + "=" + Qop : null;
            authHeader += (Opaque != null) ? "," + AuthHeaders.AUTH_OPAQUE_KEY + "=\"" + Opaque + "\"" : null;

            string algorithmID = (DigestAlgorithm == DigestAlgorithmsEnum.SHA256) ? SHA256_ALGORITHM_ID : DigestAlgorithm.ToString();
            authHeader += (Response != null) ? "," + AuthHeaders.AUTH_ALGORITHM_KEY + "=" + algorithmID : null;

            return authHeader;
        }

        public SIPAuthorisationDigest CopyOf()
        {
            var copy = new SIPAuthorisationDigest(AuthorisationType, Realm, Username, Password, URI, Nonce, RequestType, DigestAlgorithm);
            copy.Response = Response;
            copy.HA1 = HA1;
            copy.Cnonce = Cnonce;
            copy.Qop = Qop;
            copy.NonceCount = NonceCount;
            copy.Opaque = Opaque;

            return copy;
        }

        public void IncrementNonceCount()
        {
            NonceCount++;
        }

        private string GetPaddedNonceCount(int count)
        {
            return "00000000".Substring(0, 8 - NonceCount.ToString().Length) + count;
        }
    }
}
