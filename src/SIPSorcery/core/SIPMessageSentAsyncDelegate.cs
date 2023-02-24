// ============================================================================
// FileName: SIPFunctionDelegates.cs
//
// Description:
// A list of function delegates that are used by the SIP Server Agents.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 14 Nov 2008	Aaron Clauson	Created, Hobart, Australia.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// ============================================================================

using System.Threading.Tasks;

namespace SIPSorcery.SIP
{
    // SIP Channel delegates.
    public delegate Task SIPMessageSentAsyncDelegate(SIPChannel sipChannel, SIPEndPoint remoteEndPoint, byte[] buffer);

    // SIP Transport delegates.

    // SIP Transport Tracing (logging and diagnostics) delegates.

    // SIP Transaction delegates.
}
