using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Utilities;

namespace SIPSorcery.Net
{
    internal class DtlsSrtpTlsAuthentication
        : TlsAuthentication
    {
        private readonly DtlsSrtpClient mClient;
        private readonly TlsContext mContext;

        internal DtlsSrtpTlsAuthentication(DtlsSrtpClient client)
        {
            mClient = client;
            mContext = client.TlsContext;
        }

        public virtual void NotifyServerCertificate(Certificate serverCertificate)
        {
            //Console.WriteLine("DTLS client received server certificate chain of length " + chain.Length);
            mClient.ServerCertificate = serverCertificate;
        }

        public virtual TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            byte[] certificateTypes = certificateRequest.CertificateTypes;
            if (certificateTypes == null || !Arrays.Contains(certificateTypes, ClientCertificateType.rsa_sign))
            {
                return null;
            }

            return DtlsUtils.LoadSignerCredentials(mContext,
                certificateRequest.SupportedSignatureAlgorithms,
                SignatureAlgorithm.rsa,
                mClient.mCertificateChain,
                mClient.mPrivateKey);
        }
    }
}