namespace SIPSorcery.Net
{
    public enum ChunkTypeEnum : ushort
    {
        IPFamily = 0x0001,              // Payload Type: byte.
        IPProtocolID = 0x0002,          // Payload Type: byte.
        IPv4SourceAddress = 0x0003,     // Payload Type: 4 byte IPv4 address. most significant octet first.
        IPv4DesinationAddress = 0x0004, // Payload Type: same as source address. 
        IPv6SourceAddress = 0x0005,     // Payload Type: 16 byte IPv6 address. most significant octet first.
        IPv6DesinationAddress = 0x0006, // Payload Type: same as source address. 
        SourcePort = 0x0007,            // Payload Type: ushort.
        DestinationPort = 0x0008,       // Payload Type: ushort.
        TimestampSeconds = 0x0009,      // Payload Type: uint, seconds since UNIX epoch.
        TimestampMicroSeconds = 0x000a, // Payload Type: uint, offset added to timestamp seconds.
        ProtocolType = 0x000b,          // Payload Type: byte, predefined values from CaptureProtocolTypeEnum.
        CaptureAgentID = 0x000c,        // Payload Type: uint, arbitrary, used to identify agent sending packets.
        KeepAliveTimeSeconds = 0x000d,  // Payload Type: ushort.
        AuthenticationKey = 0x000e,     // Payload Type: octet-string, variable.
        CapturedPayload = 0x000f,       // Payload Type: octet-string, variable.
        // There are more types but at this point none that are useful for this library.
    }
}