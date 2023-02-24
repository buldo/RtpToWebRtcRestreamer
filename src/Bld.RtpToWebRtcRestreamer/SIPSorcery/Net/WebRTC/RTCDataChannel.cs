//-----------------------------------------------------------------------------
// Filename: RTCDataChannel.cs
//
// Description: Contains an implementation for a WebRTC data channel.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 13 Jul 2020	Aaron Clauson	Created.
// 22 Mar 2021  Aaron Clauson   Refactored for new SCTP implementation.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC
{
    /// <summary>
    /// A WebRTC data channel is generic transport service
    /// that allows peers to exchange generic data in a peer
    /// to peer manner.
    /// </summary>
    internal class RTCDataChannel
    {
        private static readonly ILogger logger = Log.Logger;

        public string label { get; set; }

        public bool negotiated { get; set; }

        public ushort? id { get; set; }

        public RTCDataChannelState readyState { get; internal set; } = RTCDataChannelState.connecting;

        public bool IsOpened { get; internal set; }

        private readonly RTCSctpTransport _transport;

        public event Action<string> onerror;
        public event Action onclose;

        public RTCDataChannel(RTCSctpTransport transport)
        {
            _transport = transport;
        }

        internal void GotAck()
        {
            logger.LogDebug($"Data channel for label {label} now open.");
            IsOpened = true;
            readyState = RTCDataChannelState.open;
        }

        /// <summary>
        /// Sends a string data payload on the data channel.
        /// </summary>
        /// <param name="message">The string message to send.</param>
        public void send(string message)
        {
            if (message != null && Encoding.UTF8.GetByteCount(message) > _transport.maxMessageSize)
            {
                throw new ApplicationException($"Data channel {label} was requested to send data of length {Encoding.UTF8.GetByteCount(message)} " +
                    $" that exceeded the maximum allowed message size of {_transport.maxMessageSize}.");
            }

            if (_transport.state != RTCSctpTransportState.Connected)
            {
                logger.LogWarning($"WebRTC data channel send failed due to SCTP transport in state {_transport.state}.");
            }
            else
            {
                lock (this)
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        _transport.RTCSctpAssociation.SendData(id.GetValueOrDefault(),
                            (uint)DataChannelPayloadProtocols.WebRTC_String_Empty,
                            new byte[] { 0x00 });
                    }
                    else
                    {
                        _transport.RTCSctpAssociation.SendData(id.GetValueOrDefault(),
                            (uint)DataChannelPayloadProtocols.WebRTC_String,
                            Encoding.UTF8.GetBytes(message));
                    }
                }
            }
        }

        /// <summary>
        /// Sends a binary data payload on the data channel.
        /// </summary>
        /// <param name="data">The data to send.</param>
        public void send(byte[] data)
        {
            if (data.Length > _transport.maxMessageSize)
            {
                throw new ApplicationException($"Data channel {label} was requested to send data of length {data.Length} " +
                    $" that exceeded the maximum allowed message size of {_transport.maxMessageSize}.");
            }

            if (_transport.state != RTCSctpTransportState.Connected)
            {
                logger.LogWarning($"WebRTC data channel send failed due to SCTP transport in state {_transport.state}.");
            }
            else
            {
                lock (this)
                {
                    if (data.Length == 0)
                    {
                        _transport.RTCSctpAssociation.SendData(id.GetValueOrDefault(),
                            (uint)DataChannelPayloadProtocols.WebRTC_Binary_Empty,
                            new byte[] { 0x00 });
                    }
                    else
                    {
                        _transport.RTCSctpAssociation.SendData(id.GetValueOrDefault(),
                            (uint)DataChannelPayloadProtocols.WebRTC_Binary,
                            data);
                    }
                }
            }
        }

        /// <summary>
        /// Sends an OPEN Data Channel Establishment Protocol (DCEP) message
        /// to open a data channel on the remote peer for send/receive.
        /// </summary>
        internal void SendDcepOpen()
        {
            var dcepOpen = new DataChannelOpenMessage
            {
                MessageType = (byte)DataChannelMessageTypes.OPEN,
                ChannelType = (byte)DataChannelTypes.DATA_CHANNEL_RELIABLE_UNORDERED,
                Label = label
            };

            lock (this)
            {
                _transport.RTCSctpAssociation.SendData(id.GetValueOrDefault(),
                       (uint)DataChannelPayloadProtocols.WebRTC_DCEP,
                       dcepOpen.GetBytes());
            }
        }

        /// <summary>
        /// Sends an ACK response for a Data Channel Establishment Protocol (DCEP)
        /// control message.
        /// </summary>
        internal void SendDcepAck()
        {
            lock (this)
            {
                _transport.RTCSctpAssociation.SendData(id.GetValueOrDefault(),
                       (uint)DataChannelPayloadProtocols.WebRTC_DCEP,
                       new[] { (byte)DataChannelMessageTypes.ACK });
            }
        }

        /// <summary>
        /// Event handler for an SCTP data chunk being received for this data channel.
        /// </summary>
        internal void GotData(uint ppID, byte[] data)
        {
            onmessage?.Invoke(this, (DataChannelPayloadProtocols)ppID, data);
        }
    }
}
