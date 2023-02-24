namespace SIPSorcery.Net
{
    public delegate void OnNewRTCDataChannel(ushort streamID, DataChannelTypes type, ushort priority, uint reliability, string label, string protocol);
}