//-----------------------------------------------------------------------------
// Filename: RtpVP8Header.cs
//
// Description: Represents the RTP header to use for a VP8 encoded payload as per
// https://tools.ietf.org/html/rfc7741.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 11 Nov 2014	Aaron Clauson	Created, Hobart, Australia.
// 11 Aug 2019  Aaron Clauson   Added full license header.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP.Packetisation
{
    /// <summary>
    /// Representation of the VP8 RTP header as specified in RFC7741
    /// https://tools.ietf.org/html/rfc7741.
    /// </summary>
    public class RtpVP8Header
    {
        // Payload Descriptor Fields.
        public bool ExtendedControlBitsPresent;     // Indicated whether extended control bits are present.
        public bool IsPictureIDPresent;
        
        private int _length;
        public int Length
        {
            get { return _length; }
        }
        
        public static RtpVP8Header GetVP8Header(byte[] rtpPayload)
        {
            RtpVP8Header vp8Header = new RtpVP8Header();

            // First byte of payload descriptor.
            vp8Header.ExtendedControlBitsPresent = ((rtpPayload[0] >> 7) & 0x01) == 1;
            vp8Header._length = 1;

            // Is second byte being used.
            if (vp8Header.ExtendedControlBitsPresent)
            {
                vp8Header.IsPictureIDPresent = ((rtpPayload[1] >> 7) & 0x01) == 1;
                vp8Header._length = 2;
            }

            // Is the picture ID being used.
            if (vp8Header.IsPictureIDPresent)
            {
                if (((rtpPayload[2] >> 7) & 0x01) == 1)
                {
                    // The Picture ID is using two bytes.
                    vp8Header._length = 4;
                }
                else
                {
                    // The picture ID is using one byte.
                    vp8Header._length = 3;
                }
            }
            
            return vp8Header;
        }
    }
}
