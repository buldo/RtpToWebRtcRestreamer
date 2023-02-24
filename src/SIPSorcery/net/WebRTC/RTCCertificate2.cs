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
    public class RTCCertificate2
    {
        public Org.BouncyCastle.X509.X509Certificate Certificate;

        public Org.BouncyCastle.Crypto.AsymmetricKeyParameter PrivateKey;
    }
}