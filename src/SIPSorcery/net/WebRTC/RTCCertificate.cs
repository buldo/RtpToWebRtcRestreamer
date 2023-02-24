using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// Represents a certificate used to authenticate WebRTC communications.
    /// </summary>
    /// <remarks>
    /// TODO:
    /// From https://www.w3.org/TR/webrtc/#methods-4:
    /// "Implementations SHOULD store the sensitive keying material in a secure module safe from 
    /// same-process memory attacks."
    /// </remarks>
    [Obsolete("Use RTCCertificate2 instead")]
    public class RTCCertificate
    {
        /// <summary>
        /// The expires attribute indicates the date and time in milliseconds relative to 1970-01-01T00:00:00Z 
        /// after which the certificate will be considered invalid by the browser.
        /// </summary>
        public long expires
        {
            get
            {
                if (Certificate == null)
                {
                    return 0;
                }
                else
                {
                    return Certificate.NotAfter.GetEpoch();
                }
            }
        }

        public X509Certificate2 Certificate;

        public List<RTCDtlsFingerprint> getFingerprints()
        {
            return new List<RTCDtlsFingerprint> { DtlsUtils.Fingerprint(Certificate) };
        }
    }
}