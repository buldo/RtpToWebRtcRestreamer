namespace SIPSorcery.SIP
{
    public delegate void SIPTransportSIPBadMessageDelegate(SIPEndPoint localSIPEndPoint, SIPEndPoint remotePoint, string message, SIPValidationFieldsEnum errorField, string rawMessage);
}