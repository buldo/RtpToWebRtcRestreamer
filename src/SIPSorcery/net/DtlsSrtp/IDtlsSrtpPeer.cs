using System;
using Org.BouncyCastle.Crypto.Tls;

namespace SIPSorcery.Net
{
    public interface IDtlsSrtpPeer
    {
        event Action<AlertLevelsEnum, AlertTypesEnum, string> OnAlert;
        bool ForceUseExtendedMasterSecret { get; set; }
        SrtpPolicy GetSrtpPolicy();
        SrtpPolicy GetSrtcpPolicy();
        byte[] GetSrtpMasterServerKey();
        byte[] GetSrtpMasterServerSalt();
        byte[] GetSrtpMasterClientKey();
        byte[] GetSrtpMasterClientSalt();
        bool IsClient();
        Certificate GetRemoteCertificate();
    }
}