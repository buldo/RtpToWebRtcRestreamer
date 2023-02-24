using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    /// <summary>
    /// For SIP URI user portion the reserved characters below need to be escaped.
    /// 
    /// <code>
    /// <![CDATA[
    /// reserved    =  ";" / "/" / "?" / ":" / "@" / "&" / "=" / "+"  / "$" / ","
    /// user-unreserved  =  "&" / "=" / "+" / "$" / "," / ";" / "?" / "/"
    /// Leaving to be escaped = ":" / "@" 
    /// ]]>
    /// </code>
    /// 
    /// For SIP URI parameters different characters are unreserved (just to make life difficult).
    /// <code>
    /// <![CDATA[
    /// reserved    =  ";" / "/" / "?" / ":" / "@" / "&" / "=" / "+"  / "$" / ","
    /// param-unreserved = "[" / "]" / "/" / ":" / "&" / "+" / "$"
    /// Leaving to be escaped =  ";" / "?" / "@" / "=" / ","
    /// ]]>
    /// </code>
    /// </summary>
    public static class SIPEscape
    {
        public static string SIPURIUserEscape(string unescapedString)
        {
            string result = unescapedString;
            if (!result.IsNullOrBlank())
            {
                result = result.Replace(":", "%3A");
                result = result.Replace("@", "%40");
                result = result.Replace(" ", "%20");
            }
            return result;
        }

        public static string SIPURIUserUnescape(string escapedString)
        {
            string result = escapedString;
            if (!result.IsNullOrBlank())
            {
                result = result.Replace("%3A", ":");
                result = result.Replace("%3a", ":");
                result = result.Replace("%20", " ");
            }
            return result;
        }

        public static string SIPURIParameterEscape(string unescapedString)
        {
            string result = unescapedString;
            if (!result.IsNullOrBlank())
            {
                result = result.Replace(";", "%3B");
                result = result.Replace("?", "%3F");
                result = result.Replace("@", "%40");
                result = result.Replace("=", "%3D");
                result = result.Replace(",", "%2C");
                result = result.Replace(" ", "%20");
            }
            return result;
        }

        public static string SIPURIParameterUnescape(string escapedString)
        {
            string result = escapedString;
            if (!result.IsNullOrBlank())
            {
                result = result.Replace("%3B", ";");
                result = result.Replace("%3b", ";");
                //result = result.Replace("%2F", "/");
                //result = result.Replace("%2f", "/");
                result = result.Replace("%3F", "?");
                result = result.Replace("%3f", "?");
                //result = result.Replace("%3A", ":");
                //result = result.Replace("%3a", ":");
                result = result.Replace("%40", "@");
                //result = result.Replace("%26", "&");
                result = result.Replace("%3D", "=");
                result = result.Replace("%3d", "=");
                //result = result.Replace("%2B", "+");
                //result = result.Replace("%2b", "+");
                //result = result.Replace("%24", "$");
                result = result.Replace("%2C", ",");
                result = result.Replace("%2c", ",");
                result = result.Replace("%20", " ");
            }
            return result;
        }
    }
}