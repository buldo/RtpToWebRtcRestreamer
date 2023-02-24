namespace SIPSorcery.Net
{
    public delegate void OnDataChannelMessageDelegate(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data);
}