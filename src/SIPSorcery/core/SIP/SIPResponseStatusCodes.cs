using System;

namespace SIPSorcery.SIP
{
    public static class SIPResponseStatusCodes
    {
        public static SIPResponseStatusCodesEnum GetStatusTypeForCode(int statusCode)
        {
            return (SIPResponseStatusCodesEnum)Enum.Parse(typeof(SIPResponseStatusCodesEnum), statusCode.ToString(), true);
        }
    }
}