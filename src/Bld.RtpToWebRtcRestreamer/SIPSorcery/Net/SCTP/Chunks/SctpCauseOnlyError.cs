//-----------------------------------------------------------------------------
// Filename: SctpErrorCauses.cs
//
// Description: Represents the SCTP error causes and the different representations
// for each error type.
//
// Remarks:
// Defined in section 3.3.10 of RFC4960:
// https://tools.ietf.org/html/rfc4960#section-3.3.10
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 01 Apr 2021	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP.Chunks;

/// <summary>
/// This structure captures all SCTP errors that don't have an additional 
/// parameter.
/// </summary>
/// <remarks>
/// Out of Resource: https://tools.ietf.org/html/rfc4960#section-3.3.10.4
/// Invalid Mandatory Parameter: https://tools.ietf.org/html/rfc4960#section-3.3.10.7
/// Cookie Received While Shutting Down: https://tools.ietf.org/html/rfc4960#section-3.3.10.10
/// </remarks>
internal struct SctpCauseOnlyError : ISctpErrorCause
{
    private const ushort ERROR_CAUSE_LENGTH = 4;

    private static readonly List<SctpErrorCauseCode> SupportedErrorCauses =
        new List<SctpErrorCauseCode>
        {
            SctpErrorCauseCode.OutOfResource,
            SctpErrorCauseCode.InvalidMandatoryParameter,
            SctpErrorCauseCode.CookieReceivedWhileShuttingDown
        };

    public SctpErrorCauseCode CauseCode { get; private set; }

    public SctpCauseOnlyError(SctpErrorCauseCode causeCode)
    {
        if (!SupportedErrorCauses.Contains(causeCode))
        {
            throw new ApplicationException($"SCTP error struct should not be used for {causeCode}, use the specific error type.");
        }

        CauseCode = causeCode;
    }

    public ushort GetErrorCauseLength(bool padded) => ERROR_CAUSE_LENGTH;

    public int WriteTo(byte[] buffer, int posn)
    {
        NetConvert.ToBuffer((ushort)CauseCode, buffer, posn);
        NetConvert.ToBuffer(ERROR_CAUSE_LENGTH, buffer, posn + 2);
        return ERROR_CAUSE_LENGTH;
    }
}