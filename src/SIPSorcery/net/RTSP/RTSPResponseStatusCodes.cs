using System;

namespace SIPSorcery.Net
{
    public class RTSPResponseStatusCodes
    {
        public static RTSPResponseStatusCodesEnum GetStatusTypeForCode(int statusCode)
        {
            return (RTSPResponseStatusCodesEnum)Enum.Parse(typeof(RTSPResponseStatusCodesEnum), statusCode.ToString(), true);
        }
    }
}