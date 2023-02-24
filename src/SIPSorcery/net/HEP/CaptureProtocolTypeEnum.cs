namespace SIPSorcery.Net
{
    public enum CaptureProtocolTypeEnum : byte
    {
        Reserved = 0x00,
        SIP = 0x01,
        XMPP = 0x02,
        SDP = 0x03,
        RTP = 0x04,
        RTCP_JSON = 0x05,
        // There are more types but at this point none that are useful for this library.
    }
}