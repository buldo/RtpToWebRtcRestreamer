using System;

namespace SIPSorcery.Net
{
    public class RTSPMethods
    {
        public static RTSPMethodsEnum GetMethod(string method)
        {
            RTSPMethodsEnum rtspMethod = RTSPMethodsEnum.UNKNOWN;

            try
            {
                rtspMethod = (RTSPMethodsEnum)Enum.Parse(typeof(RTSPMethodsEnum), method, true);
            }
            catch { }

            return rtspMethod;
        }
    }
}