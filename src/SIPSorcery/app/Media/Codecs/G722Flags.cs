using System;

namespace SIPSorcery.Media
{
    /// <summary>
    /// G722 Flags
    /// </summary>
    [Flags]
    public enum G722Flags
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// Using a G722 sample rate of 8000
        /// </summary>
        SampleRate8000 = 0x0001,
        /// <summary>
        /// Packed
        /// </summary>
        Packed = 0x0002
    }
}