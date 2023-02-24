using System;

namespace SIPSorcery.Media
{
    /// <summary>
    /// Stores state to be used between calls to Encode or Decode
    /// </summary>
    public class G722CodecState
    {
        /// <summary>
        /// ITU Test Mode
        /// TRUE if the operating in the special ITU test mode, with the band split filters disabled.
        /// </summary>
        public bool ItuTestMode { get; set; }

        /// <summary>
        /// TRUE if the G.722 data is packed
        /// </summary>
        public bool Packed { get; private set; }

        /// <summary>
        /// 8kHz Sampling
        /// TRUE if encode from 8k samples/second
        /// </summary>
        public bool EncodeFrom8000Hz { get; private set; }

        /// <summary>
        /// Bits Per Sample
        /// 6 for 48000kbps, 7 for 56000kbps, or 8 for 64000kbps.
        /// </summary>
        public int BitsPerSample { get; private set; }

        /// <summary>
        /// Signal history for the QMF (x)
        /// </summary>
        public int[] QmfSignalHistory { get; private set; }

        /// <summary>
        /// Band
        /// </summary>
        public Band[] Band { get; private set; }

        /// <summary>
        /// In bit buffer
        /// </summary>
        public uint InBuffer { get; internal set; }

        /// <summary>
        /// Number of bits in InBuffer
        /// </summary>
        public int InBits { get; internal set; }

        /// <summary>
        /// Out bit buffer
        /// </summary>
        public uint OutBuffer { get; internal set; }

        /// <summary>
        /// Number of bits in OutBuffer
        /// </summary>
        public int OutBits { get; internal set; }

        /// <summary>
        /// Creates a new instance of G722 Codec State for a 
        /// new encode or decode session
        /// </summary>
        /// <param name="rate">Bitrate (typically 64000)</param>
        /// <param name="options">Special options</param>
        public G722CodecState(int rate, G722Flags options)
        {
            this.Band = new Band[2] { new Band(), new Band() };
            this.QmfSignalHistory = new int[24];
            this.ItuTestMode = false;

            if (rate == 48000)
            {
                this.BitsPerSample = 6;
            }
            else if (rate == 56000)
            {
                this.BitsPerSample = 7;
            }
            else if (rate == 64000)
            {
                this.BitsPerSample = 8;
            }
            else
            {
                throw new ArgumentException("Invalid rate, should be 48000, 56000 or 64000");
            }

            if ((options & G722Flags.SampleRate8000) == G722Flags.SampleRate8000)
            {
                this.EncodeFrom8000Hz = true;
            }

            if (((options & G722Flags.Packed) == G722Flags.Packed) && this.BitsPerSample != 8)
            {
                this.Packed = true;
            }
            else
            {
                this.Packed = false;
            }

            this.Band[0].det = 32;
            this.Band[1].det = 8;
        }
    }
}