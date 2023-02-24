using System;

namespace SIPSorcery.net.RTP
{
    public class DefaultTimeProvider : IDateTime
    {
        public DateTime Time => DateTime.Now;
    }
}