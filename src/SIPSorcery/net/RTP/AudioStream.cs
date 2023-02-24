//-----------------------------------------------------------------------------
// Filename: AudioStream.cs
//
// Description: Define an Audio media stream (which inherits MediaStream) to focus an Audio specific treatment
// The goal is to simplify RTPSession class
//
// Author(s):
// Christophe Irles
//
// History:
// 05 Apr 2022	Christophe Irles        Created (based on existing code from previous RTPSession class)
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.Sys;
using SIPSorceryMedia.Abstractions;

namespace SIPSorcery.net.RTP
{
    public class AudioStream : MediaStream
    {
        #region EVENTS

        /// <summary>
        /// Gets fired when the remote SDP is received and the set of common audio formats is set.
        /// </summary>
        public event Action<int, List<AudioFormat>> OnAudioFormatsNegotiatedByIndex;

        #endregion EVENTS

        #region PROPERTIES

        /// <summary>
        /// Indicates whether this session is using audio.
        /// </summary>
        public bool HasAudio
        {
            get
            {
                return LocalTrack != null && LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive
                  && RemoteTrack != null && RemoteTrack.StreamStatus != MediaStreamStatusEnum.Inactive;
            }
        }

        #endregion PROPERTIES

        public void CheckAudioFormatsNegotiation()
        {
            if (LocalTrack != null &&
                        LocalTrack.Capabilities.Where(x => x.Name().ToLower() != SDP.TELEPHONE_EVENT_ATTRIBUTE).Count() > 0)
            {
                OnAudioFormatsNegotiatedByIndex?.Invoke(
                            Index,
                            LocalTrack.Capabilities
                            .Where(x => x.Name().ToLower() != SDP.TELEPHONE_EVENT_ATTRIBUTE)
                            .Select(x => x.ToAudioFormat()).ToList());
            }
        }

        public AudioStream(RtpSessionConfig config, int index) : base(config, index)
        {
            MediaType = SDPMediaTypesEnum.audio;
        }
    }
}
