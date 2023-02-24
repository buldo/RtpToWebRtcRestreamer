using System;
using System.Security.Cryptography;
using System.Text;
using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    public class HTTPDigest
    {
        /// <summary>
        /// Calculate H(A1) as per HTTP Digest specification.
        /// </summary>
        public static string DigestCalcHA1(
            string username,
            string realm,
            string password,
            DigestAlgorithmsEnum hashAlg = DigestAlgorithmsEnum.MD5)
        {
            string a1 = String.Format("{0}:{1}:{2}", username, realm, password);
            return GetHashHex(hashAlg, a1);
        }

        /// <summary>
        /// Calculate H(A2) as per HTTP Digest specification.
        /// </summary>
        public static string DigestCalcHA2(
            string method,
            string uri,
            DigestAlgorithmsEnum hashAlg = DigestAlgorithmsEnum.MD5)
        {
            string A2 = String.Format("{0}:{1}", method, uri);

            return GetHashHex(hashAlg, A2);
        }

        public static string DigestCalcResponse(
            string username,
            string realm,
            string password,
            string uri,
            string nonce,
            string nonceCount,
            string cnonce,
            string qop,         // qop-value: "", "auth", "auth-int".
            string method,
            DigestAlgorithmsEnum hashAlg = DigestAlgorithmsEnum.MD5)
        {
            string HA1 = DigestCalcHA1(username, realm, password, hashAlg);
            return DigestCalcResponse(HA1, uri, nonce, nonceCount, cnonce, qop, method, hashAlg);
        }

        public static string DigestCalcResponse(
            string ha1,
            string uri,
            string nonce,
            string nonceCount,
            string cnonce,
            string qop,         // qop-value: "", "auth", "auth-int".
            string method,
            DigestAlgorithmsEnum hashAlg = DigestAlgorithmsEnum.MD5)
        {
            string HA2 = DigestCalcHA2(method, uri, hashAlg);

            string unhashedDigest = null;
            if (nonceCount != null && cnonce != null && qop != null)
            {
                unhashedDigest = String.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                    ha1,
                    nonce,
                    nonceCount,
                    cnonce,
                    qop,
                    HA2);
            }
            else
            {
                unhashedDigest = String.Format("{0}:{1}:{2}",
                    ha1,
                    nonce,
                    HA2);
            }

            return GetHashHex(hashAlg, unhashedDigest);
        }

        public static string GetHashHex(DigestAlgorithmsEnum hashAlg, string val)
        {
            // TODO: When .NET Standard and Framework support are deprecated this pragma can be removed.
#pragma warning disable SYSLIB0021
            switch (hashAlg)
            {
                case DigestAlgorithmsEnum.SHA256:
                    using (var hash = new SHA256CryptoServiceProvider())
                    {
                        return hash.ComputeHash(Encoding.UTF8.GetBytes(val)).HexStr().ToLower();
                    }
                // This is commented because RFC8760 does not have an SHA-512 option. Instead it's HSA-512-sess which
                // means the SIP request body needs to be included in the digest as well. Including the body will require 
                // some additional changes that can be done at a later date.
                //case DigestAlgorithmsEnum.SHA512:
                //    using (var hash = new SHA512CryptoServiceProvider())
                //    {
                //        return hash.ComputeHash(Encoding.UTF8.GetBytes(val)).HexStr().ToLower();
                //    }
                case DigestAlgorithmsEnum.MD5:
                default:
                    using (var hash = new MD5CryptoServiceProvider())
                    {
                        return hash.ComputeHash(Encoding.UTF8.GetBytes(val)).HexStr().ToLower();
                    }
            }
#pragma warning restore SYSLIB0021
        }
    }
}