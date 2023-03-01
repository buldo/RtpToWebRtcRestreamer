namespace Bld.Rtp;

public record RtpHeader
{
    private const int RTP_VERSION = 2;

    public int Version { get;  set; } = RTP_VERSION; // 2 bits.
    public int PaddingFlag { get; set; } // 1 bit.
    public int HeaderExtensionFlag { get; set; } // 1 bit.
    public int CSRCCount { get; set; } // 4 bits
    public int MarkerBit { get; set; } // 1 bit.
    public int PayloadType { get; set; } // 7 bits.
    public UInt16 SequenceNumber { get; set; } // 16 bits.
    public uint Timestamp { get; set; } // 32 bits.
    public uint SyncSource { get; set; } // 32 bits.
    public UInt16 ExtensionProfile { get; set; } // 16 bits.
    public UInt16 ExtensionLength { get; set; } // 16 bits, length of the header extensions in 32 bit words.
    public byte[] ExtensionPayload { get; set; }

    public int PayloadSize { get; set; }
    public byte PaddingCount { get; set; }
    public DateTime ReceivedTime { get; set; }

    public int Length
    {
        get
        {
            return RtpHeaderSerializer.MIN_HEADER_LEN + (CSRCCount * 4) +
                   ((HeaderExtensionFlag == 0) ? 0 : 4 + (ExtensionLength * 4));
        }
    }
}