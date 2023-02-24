using System;

namespace SIPSorcery.SIP
{
    public static class SIPMethods
    {
        public static SIPMethodsEnum GetMethod(string method)
        {
            SIPMethodsEnum sipMethod = SIPMethodsEnum.UNKNOWN;

            try
            {
                sipMethod = (SIPMethodsEnum)Enum.Parse(typeof(SIPMethodsEnum), method, true);
            }
            catch { }

            return sipMethod;
        }
    }
}