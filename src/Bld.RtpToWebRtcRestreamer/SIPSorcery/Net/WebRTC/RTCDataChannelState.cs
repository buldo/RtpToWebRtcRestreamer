using System.Diagnostics.CodeAnalysis;

namespace SIPSorcery.Net
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum RTCDataChannelState
    {
        /// <summary>
        /// The user agent is attempting to establish the underlying data transport. 
        /// This is the initial state of an RTCDataChannel object, whether created 
        /// with createDataChannel, or dispatched as a part of an RTCDataChannelEvent.
        /// </summary>
        connecting,

        /// <summary>
        /// The underlying data transport is established and communication is possible.
        /// </summary>
        open,

        /// <summary>
        /// The procedure to close down the underlying data transport has started.
        /// </summary>
        closing,

        /// <summary>
        /// The underlying data transport has been closed or could not be established.
        /// </summary>
        closed
    }
}