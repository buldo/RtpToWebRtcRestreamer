using SIPSorceryMedia.Abstractions;

namespace SIPSorcery.Media
{
    public class AudioSourceOptions
    {
        /// <summary>
        /// The type of audio source to use.
        /// </summary>
        public AudioSourcesEnum AudioSource;

        /// <summary>
        /// The sampling rate used to generate the input or if the source is
        /// being generated the sample rate to generate it at.
        /// </summary>
        public AudioSamplingRatesEnum MusicInputSamplingRate = AudioSamplingRatesEnum.Rate8KHz;

        /// <summary>
        /// If the audio source is set to music this must be the path to a raw PCM 8K sampled file.
        /// If set to null or the file doesn't exist the default embedded resource music file will
        /// be used.
        /// </summary>
        public string MusicFile;
    }
}