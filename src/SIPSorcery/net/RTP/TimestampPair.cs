﻿namespace SIPSorcery.Net
{
    public class TimestampPair
    {
        public uint RtpTimestamp { get; set; }
        public ulong NtpTimestamp { get; set; }
    }
}