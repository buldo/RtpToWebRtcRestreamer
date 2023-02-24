using System;

namespace SIPSorcery.SIP
{
    /// <summary>
    /// Represents a message received on a SIP channel prior to any attempt to identify
    /// whether it represents a SIP request, response or something else.
    /// </summary>
    internal class IncomingMessage
    {
        /// <summary>
        /// The SIP channel we received the message on.
        /// </summary>
        public SIPChannel LocalSIPChannel;

        /// <summary>
        /// The local end point that the message was received on. If a SIP channel
        /// is listening on IPAddress.Any then this property will hold the actual 
        /// IP address that was used for the receive.
        /// </summary>
        public SIPEndPoint LocalEndPoint;

        /// <summary>
        /// The next hop remote SIP end point the message came from.
        /// </summary>
        public SIPEndPoint RemoteEndPoint;

        /// <summary>
        /// The message data.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// The time at which the message was received.
        /// </summary>
        public DateTime ReceivedAt;

        public IncomingMessage(SIPChannel sipChannel, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, byte[] buffer)
        {
            LocalSIPChannel = sipChannel;
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
            Buffer = buffer;
            ReceivedAt = DateTime.Now;
        }
    }
}