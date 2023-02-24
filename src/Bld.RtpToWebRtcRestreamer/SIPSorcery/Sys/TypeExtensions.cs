//-----------------------------------------------------------------------------
// Filename: TypeExtensions.cs
//
// Description: Helper methods.
//
// Author(s):
// Aaron Clauson
//
// History:
// ??	Aaron Clauson	Created.
// 21 Jan 2020  Aaron Clauson   Added HexStr and ParseHexStr (borrowed from
//                              Bitcoin Core source).
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys
{
    public static class TypeExtensions
    {
        // The Trim method only trims 0x0009, 0x000a, 0x000b, 0x000c, 0x000d, 0x0085, 0x2028, and 0x2029.
        // This array adds in control characters.
        private static readonly char[] WhiteSpaceChars = { (char)0x00, (char)0x01, (char)0x02, (char)0x03, (char)0x04, (char)0x05,
            (char)0x06, (char)0x07, (char)0x08, (char)0x09, (char)0x0a, (char)0x0b, (char)0x0c, (char)0x0d, (char)0x0e, (char)0x0f,
            (char)0x10, (char)0x11, (char)0x12, (char)0x13, (char)0x14, (char)0x15, (char)0x16, (char)0x17, (char)0x18, (char)0x19, (char)0x20,
            (char)0x1a, (char)0x1b, (char)0x1c, (char)0x1d, (char)0x1e, (char)0x1f, (char)0x7f, (char)0x85, (char)0x2028, (char)0x2029 };

        private static readonly char[] Hexmap = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        /// <summary>    
        /// Gets a value that indicates whether or not the collection is empty.    
        /// </summary>    
        public static bool IsNullOrBlank(this string s)
        {
            if (s == null || s.Trim(WhiteSpaceChars).Length == 0)
            {
                return true;
            }

            return false;
        }

        public static bool NotNullOrBlank(this string s)
        {
            if (s == null || s.Trim(WhiteSpaceChars).Length == 0)
            {
                return false;
            }

            return true;
        }

        public static string HexStr(this byte[] buffer, char? separator = null)
        {
            return buffer.HexStr(buffer.Length, separator);
        }

        private static string HexStr(this byte[] buffer, int length, char? separator = null)
        {
            var rv = string.Empty;

            for (var i = 0; i < length; i++)
            {
                var val = buffer[i];
                rv += Hexmap[val >> 4];
                rv += Hexmap[val & 15];

                if (separator != null && i != length - 1)
                {
                    rv += separator;
                }
            }

            return rv.ToUpper();
        }

        /// <summary>
        /// Purpose of this extension is to allow deconstruction of a list into a fixed size tuple.
        /// </summary>
        /// <example>
        /// (var field0, var field1, var field2) = "a b c".Split();
        /// </example>
        public static void Deconstruct<T>(this IList<T> list, out T first, out T second, out T third)
        {
            first = list.Count > 0 ? list[0] : default(T);
            second = list.Count > 1 ? list[1] : default(T);
            third = list.Count > 2 ? list[2] : default(T);
        }
    }
}
