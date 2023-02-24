using System;

namespace SIPSorcery.SIP
{
    public static class SIPSchemesType
    {
        public static SIPSchemesEnum GetSchemeType(string schemeType)
        {
            return (SIPSchemesEnum)Enum.Parse(typeof(SIPSchemesEnum), schemeType, true);
        }

        public static bool IsAllowedScheme(string schemeType)
        {
            try
            {
                Enum.Parse(typeof(SIPSchemesEnum), schemeType, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}