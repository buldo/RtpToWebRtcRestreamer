// ============================================================================
// FileName: SIPEventPackages.cs
//
// Description:
// Data structures and types related to RFC3265 "Session Initiation Protocol
// (SIP)-Specific Event Notification".
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 23 Feb 2010	Aaron Clauson	Created, Hobart, Australia.
// 01 Feb 2021  Aaron Clauson   Simplified parsing of the event package type.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// ============================================================================

namespace SIPSorcery.SIP
{
    public static class SIPEventConsts
    {
        public const string DIALOG_XML_NAMESPACE_URN = "urn:ietf:params:xml:ns:dialog-info";
        public const string PIDF_XML_NAMESPACE_URN = "urn:ietf:params:xml:ns:pidf";             // Presence Information Data Format XML namespace.
    }
}
