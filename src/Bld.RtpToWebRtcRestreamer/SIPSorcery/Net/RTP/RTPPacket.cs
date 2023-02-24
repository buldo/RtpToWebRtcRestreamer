//-----------------------------------------------------------------------------
// Filename: RTPPacket.cs
//
// Description: Encapsulation of an RTP packet.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 24 May 2005	Aaron Clauson 	Created, Dublin, Ireland.
// 11 Aug 2019  Aaron Clauson   Added full license header.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP
{
    public class RTPPacket
    {
        public readonly RTPHeader Header;
        public readonly byte[] Payload;

        public RTPPacket(int payloadSize)
        {
            Header = new RTPHeader();
            Payload = new byte[payloadSize];
        }

        public RTPPacket(byte[] packet)
        {
            Header = new RTPHeader(packet);
            Payload = new byte[Header.PayloadSize];
            Array.Copy(packet, Header.Length, Payload, 0, Payload.Length);
        }

        public byte[] GetBytes()
        {
            var header = Header.GetBytes();
            var packet = new byte[header.Length + Payload.Length];

            Array.Copy(header, packet, header.Length);
            Array.Copy(Payload, 0, packet, header.Length, Payload.Length);

            return packet;
        }
    }
}
