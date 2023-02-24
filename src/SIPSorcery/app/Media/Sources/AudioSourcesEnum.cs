namespace SIPSorcery.Media
{
    public enum AudioSourcesEnum
    {
        /// <summary>
        /// Plays music samples from a file. The file will be played in a loop until
        /// another source option is set.
        /// </summary>
        Music = 0,

        /// <summary>
        /// Send an audio stream of silence. Note this option does result
        /// in audio RTP packet getting sent.
        /// </summary>
        Silence = 1,

        /// <summary>
        /// White noise static.
        /// </summary>
        WhiteNoise = 2,

        /// <summary>
        /// A continuous sine wave.
        /// </summary>
        SineWave = 3,

        /// <summary>
        /// Pink noise static.
        /// </summary>
        PinkNoise = 4,

        /// <summary>
        /// Don't generate any audio samples.
        /// </summary>
        None = 5,
    }
}