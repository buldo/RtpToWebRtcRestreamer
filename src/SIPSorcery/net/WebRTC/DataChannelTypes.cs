namespace SIPSorcery.Net
{
    public enum DataChannelTypes : byte
    {
        /// <summary>
        /// The data channel provides a reliable in-order bidirectional communication.
        /// </summary>
        DATA_CHANNEL_RELIABLE = 0x00,

        /// <summary>
        /// The data channel provides a partially reliable in-order bidirectional
        /// communication. User messages will not be retransmitted more
        /// times than specified in the Reliability Parameter
        /// </summary>
        DATA_CHANNEL_PARTIAL_RELIABLE_REXMIT = 0x01,

        /// <summary>
        /// The data channel provides a partially reliable in-order bidirectional
        /// communication. User messages might not be transmitted or
        /// retransmitted after a specified lifetime given in milliseconds
        /// in the Reliability Parameter. This lifetime starts when
        /// providing the user message to the protocol stack.
        /// </summary>
        DATA_CHANNEL_PARTIAL_RELIABLE_TIMED = 0x02,

        /// <summary>
        /// The data channel provides a reliable unordered bidirectional communication.
        /// </summary>
        DATA_CHANNEL_RELIABLE_UNORDERED = 0x80,

        /// <summary>
        /// The data channel provides a partially reliable unordered bidirectional
        /// communication. User messages will not be retransmitted more
        /// times than specified in the Reliability Parameter.
        /// </summary>
        DATA_CHANNEL_PARTIAL_RELIABLE_REXMIT_UNORDERED = 0x81,

        /// <summary>
        /// The data channel provides a partially reliable unordered bidirectional
        /// communication. User messages might not be transmitted or
        /// retransmitted after a specified lifetime given in milliseconds
        /// in the Reliability Parameter. This lifetime starts when
        /// providing the user message to the protocol stack.
        /// </summary>
        DATA_CHANNEL_PARTIAL_RELIABLE_TIMED_UNORDERED = 0x82
    }
}