//-----------------------------------------------------------------------------
// Filename: RTSPConstants.cs
//
// Description: RTSP constants.
//
// Author(s):
// Aaron Clauson
//
// History:
// 09 Nov 2007	Aaron Clauson	Created (aaron@sipsorcery.com), SIP Sorcery PTY LTD, Hobart, Australia (www.sipsorcery.com).
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace SIPSorcery.Net
{
    public class RTSPConstants
    {
        public const string CRLF = "\r\n";

        public const string RTSP_VERSION_STRING = "RTSP";
        public const int RTSP_MAJOR_VERSION = 1;
        public const int RTSP_MINOR_VERSION = 0;
        public const string RTSP_FULLVERSION_STRING = "RTSP/1.0";

        public const int DEFAULT_RTSP_PORT = 554;                       // RFC2326 9.2, default port for both TCP and UDP.
        public const string RTSP_RELIABLE_TRANSPORTID = "rtsp";
        public const string RTSP_UNRELIABLE_TRANSPORTID = "rtspu";

        public const int INITIAL_RTT_MILLISECONDS = 500;                // RFC2326 9.2, initial round trip time used for retransmits on unreliable transports.
        public const int RTSP_MAXIMUM_LENGTH = 4096;
    }
}
