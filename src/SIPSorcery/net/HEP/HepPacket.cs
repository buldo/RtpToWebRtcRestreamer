//-----------------------------------------------------------------------------
// Filename: HepPacket.cs
//
// Description: Homer Encapsulation Protocol packet for the HOMER SIP
// capture and logging server (sipcapture.org). Specification for the packet
// format is available at https://github.com/sipcapture/HEP. The purpose of
// protocol is:
// "...provides a method to duplicate an IP datagram to a collector by 
// encapsulating the original datagram and its relative header properties 
// within a new IP datagram transmitted over UDP/TCP/SCTP connections 
// for remote collection." 
//
// Note: The web site and docs make reference to the name changing from
// Homer Encapsulation Protocol (HEP) to Extensible Encapsulation 
// Protocol (EEP) but the new name is not used in the main specification 
// or Asteriskv17.0.1 or the HOMERv7 server.
//
// Implementation Note: Based on https://github.com/sipcapture/hep-c
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 02 Dec 2019	Aaron Clauson	Created for HEPv3, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using SIPSorcery.SIP;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// This class can produce a serialised Homer Encapsulation Protocol (HEP) packet. The implementation
    /// has only been done to accommodate packet types required by this library (at the time of writing 
    /// the sole type is SIP).
    /// </summary>
    public class HepPacket
    {
        private const int MAX_HEP_PACKET_LENGTH = 1460;

        /// <summary>
        /// All the SIP protocols except UDP use TCP as the underlying transport protocol.
        /// </summary>
        private static byte GetProtocolNumber(SIPProtocolsEnum sipProtocol)
        {
            switch (sipProtocol)
            {
                case SIPProtocolsEnum.udp:
                    return (byte)ProtocolType.Udp;
                default:
                    return (byte)ProtocolType.Tcp;
            }
        }

        /// <summary>
        /// Gets a serialised HEP packet for a SIP request or response that can be sent to a HOMER server.
        /// </summary>
        /// <param name="srcEndPoint">The end point that sent the SIP request or response.</param>
        /// <param name="dstEndPoint">The end point that the SIP request or response was sent to.</param>
        /// <param name="timestamp">The timestamp the request or response was generated.</param>
        /// <param name="agentID">An agent ID that is used by the HOMER server to identify the agent generating 
        /// HEP packets. Ideally should be unique amongst all agents logging to the same HOMER server.</param>
        /// <param name="password">The password required by the HOMER server. Can be set to null if no password
        /// is required. Default value for HOMER5 and 7 is 'myHep".</param>
        /// <param name="payload">The SIP request or response.</param>
        /// <returns>An array of bytes representing the serialised HEP packet and that is ready for transmission
        /// to a HOMER server.</returns>
        public static byte[] GetBytes(SIPEndPoint srcEndPoint, SIPEndPoint dstEndPoint, DateTime timestamp, uint agentID, string password, string payload)
        {
            byte[] packetBuffer = new byte[MAX_HEP_PACKET_LENGTH];
            int offset = 0;

            // HEP3 ASCII code to start the packet.
            packetBuffer[0] = 0x48;
            packetBuffer[1] = 0x45;
            packetBuffer[2] = 0x50;
            packetBuffer[3] = 0x33;

            offset = 6;

            // IP family.
            var familyChunkBuffer = HepChunk.GetBytes(ChunkTypeEnum.IPFamily, (byte)srcEndPoint.Address.AddressFamily);
            Buffer.BlockCopy(familyChunkBuffer, 0, packetBuffer, offset, familyChunkBuffer.Length);
            offset += familyChunkBuffer.Length;

            // IP transport layer protocol.
            var protocolChunkBuffer = HepChunk.GetBytes(ChunkTypeEnum.IPProtocolID, GetProtocolNumber(srcEndPoint.Protocol));
            Buffer.BlockCopy(protocolChunkBuffer, 0, packetBuffer, offset, protocolChunkBuffer.Length);
            offset += protocolChunkBuffer.Length;

            // Source IP address.
            ChunkTypeEnum srcChunkType = srcEndPoint.Address.AddressFamily == AddressFamily.InterNetwork ? ChunkTypeEnum.IPv4SourceAddress : ChunkTypeEnum.IPv6SourceAddress;
            var srcIPAddress = HepChunk.GetBytes(srcChunkType, srcEndPoint.Address);
            Buffer.BlockCopy(srcIPAddress, 0, packetBuffer, offset, srcIPAddress.Length);
            offset += srcIPAddress.Length;

            // Destination IP address.
            ChunkTypeEnum dstChunkType = dstEndPoint.Address.AddressFamily == AddressFamily.InterNetwork ? ChunkTypeEnum.IPv4DesinationAddress : ChunkTypeEnum.IPv6DesinationAddress;
            var dstIPAddress = HepChunk.GetBytes(dstChunkType, dstEndPoint.Address);
            Buffer.BlockCopy(dstIPAddress, 0, packetBuffer, offset, dstIPAddress.Length);
            offset += dstIPAddress.Length;

            // Source port.
            var srcPortBuffer = HepChunk.GetBytes(ChunkTypeEnum.SourcePort, (ushort)srcEndPoint.Port);
            Buffer.BlockCopy(srcPortBuffer, 0, packetBuffer, offset, srcPortBuffer.Length);
            offset += srcPortBuffer.Length;

            // Destination port.
            var dstPortBuffer = HepChunk.GetBytes(ChunkTypeEnum.DestinationPort, (ushort)dstEndPoint.Port);
            Buffer.BlockCopy(dstPortBuffer, 0, packetBuffer, offset, dstPortBuffer.Length);
            offset += dstPortBuffer.Length;

            // Timestamp.
            var timestampBuffer = HepChunk.GetBytes(ChunkTypeEnum.TimestampSeconds, (uint)timestamp.GetEpoch());
            Buffer.BlockCopy(timestampBuffer, 0, packetBuffer, offset, timestampBuffer.Length);
            offset += timestampBuffer.Length;

            // Timestamp micro seconds (.NET only has millisecond resolution).
            var timestampMicrosBuffer = HepChunk.GetBytes(ChunkTypeEnum.TimestampMicroSeconds, (uint)(timestamp.Millisecond * 1000));
            Buffer.BlockCopy(timestampMicrosBuffer, 0, packetBuffer, offset, timestampMicrosBuffer.Length);
            offset += timestampMicrosBuffer.Length;

            // Protocol type, only interested in SIP at this point.
            var protocolTypeBuffer = HepChunk.GetBytes(ChunkTypeEnum.ProtocolType, (byte)CaptureProtocolTypeEnum.SIP);
            Buffer.BlockCopy(protocolTypeBuffer, 0, packetBuffer, offset, protocolTypeBuffer.Length);
            offset += protocolTypeBuffer.Length;

            // Capture agent ID.
            var agentIDBuffer = HepChunk.GetBytes(ChunkTypeEnum.CaptureAgentID, agentID);
            Buffer.BlockCopy(agentIDBuffer, 0, packetBuffer, offset, agentIDBuffer.Length);
            offset += agentIDBuffer.Length;

            // Auth key
            if (!String.IsNullOrEmpty(password))
            {
                var passwordBuffer = HepChunk.GetBytes(ChunkTypeEnum.AuthenticationKey, Encoding.UTF8.GetBytes(password));
                Buffer.BlockCopy(passwordBuffer, 0, packetBuffer, offset, passwordBuffer.Length);
                offset += passwordBuffer.Length;
            }

            // Payload
            var payloadBuffer = HepChunk.GetBytes(ChunkTypeEnum.CapturedPayload, Encoding.UTF8.GetBytes(payload));

            // If we don't have enough space left truncate the payload.
            int payloadLength = (payloadBuffer.Length > packetBuffer.Length - offset) ? packetBuffer.Length - offset : payloadBuffer.Length;

            Buffer.BlockCopy(payloadBuffer, 0, packetBuffer, offset, payloadLength);
            offset += payloadLength;

            // Length
            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NetConvert.DoReverseEndian((ushort)offset)), 0, packetBuffer, 4, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes((ushort)offset), 0, packetBuffer, 4, 2);
            }

            return packetBuffer.Take(offset).ToArray();
        }
    }
}