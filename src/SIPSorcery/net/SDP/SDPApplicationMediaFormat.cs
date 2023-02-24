//-----------------------------------------------------------------------------
// Filename: SDPApplicationMediaFormat.cs
//
// Description: An SDP media format for an "application" media announcement.
// These media formats differ from those used with "audio" and "video"
// announcements.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 17 OCt 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace SIPSorcery.Net
{
    public struct SDPApplicationMediaFormat
    {
        public string ID;

        public string Rtpmap;

        public string Fmtp;

        public SDPApplicationMediaFormat(string id)
        {
            ID = id;
            Rtpmap = null;
            Fmtp = null;
        }

        public SDPApplicationMediaFormat(string id, string rtpmap, string fmtp)
        {
            ID = id;
            Rtpmap = rtpmap;
            Fmtp = fmtp;
        }

        public SDPApplicationMediaFormat WithUpdatedRtpmap(string rtpmap) =>
            new SDPApplicationMediaFormat(ID, rtpmap, Fmtp);

        public SDPApplicationMediaFormat WithUpdatedFmtp(string fmtp) =>
            new SDPApplicationMediaFormat(ID, Rtpmap, fmtp);
    }
}
