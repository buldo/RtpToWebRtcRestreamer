using System;
using System.Security.Cryptography.X509Certificates;

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
        public X509Certificate2 Certificate;
    }
}