using Org.BouncyCastle.Tls;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp
{
    internal interface IDtlsSrtpPeer
    {
        event Action<AlertLevelsEnum, AlertTypesEnum, string> OnAlert;
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