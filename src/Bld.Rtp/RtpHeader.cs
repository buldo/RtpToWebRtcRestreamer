namespace Bld.Rtp;

public class RtpHeader
{
    private const int RTP_VERSION = 2;

    public int Version { get; internal set; } = RTP_VERSION; // 2 bits.
    public int PaddingFlag { get; internal set; } // 1 bit.
    public int HeaderExtensionFlag { get; internal set; } // 1 bit.
    public int CSRCCount { get; internal set; } // 4 bits
    public int MarkerBit { get; internal set; } // 1 bit.
    public int PayloadType { get; internal set; } // 7 bits.
    public UInt16 SequenceNumber { get; internal set; } // 16 bits.
    public uint Timestamp { get; internal set; } // 32 bits.
    public uint SyncSource { get; internal set; } // 32 bits.
    public UInt16 ExtensionProfile { get; internal set; } // 16 bits.
    public UInt16 ExtensionLength { get; internal set; } // 16 bits, length of the header extensions in 32 bit words.
    public byte[] ExtensionPayload { get; internal set; }

    public int PayloadSize { get; internal set; }
    public byte PaddingCount { get; internal set; }
    public DateTime ReceivedTime { get; internal set; }

    public int Length
    {
        get
        {
            return RtpHeaderSerializer.MIN_HEADER_LEN + (CSRCCount * 4) +
                   ((HeaderExtensionFlag == 0) ? 0 : 4 + (ExtensionLength * 4));
        }
    }
}