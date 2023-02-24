﻿namespace SIPSorcery.Net
{
    /// <summary>
    /// An attribute used to defined additional properties about
    /// a media source and the relationship between them.
    /// As specified in RFC5576, https://tools.ietf.org/html/rfc5576.
    /// </summary>
    public class SDPSsrcAttribute
    {
        public const string MEDIA_CNAME_ATTRIBUE_PREFIX = "cname";

        public uint SSRC { get; set; }

        public string Cname { get; set; }

        public string GroupID { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="ssrc">The SSRC that should match an RTP stream.</param>
        /// <param name="cname">Optional. The CNAME value to use in RTCP SDES sections.</param>
        /// <param name="groupID">Optional. If this "ssrc" attribute is part of a 
        /// group this is the group ID.</param>
        public SDPSsrcAttribute(uint ssrc, string cname, string groupID)
        {
            SSRC = ssrc;
            Cname = cname;
            GroupID = groupID;
        }
    }
}