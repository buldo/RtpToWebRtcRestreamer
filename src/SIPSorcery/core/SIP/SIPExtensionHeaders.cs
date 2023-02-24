using System;
using System.Collections.Generic;

namespace SIPSorcery.SIP
{
    /// <summary>
    /// Constants that can be placed in the SIP Supported or Required headers to indicate support or mandate for
    /// a particular SIP extension.
    /// </summary>
    public static class SIPExtensionHeaders
    {
        public const string PRACK = "100rel";
        public const string NO_REFER_SUB = "norefersub";
        public const string REPLACES = "replaces";
        public const string SIPREC = "siprec";
        public const string MULTIPLE_REFER = "multiple-refer";

        /// <summary>
        /// Parses a string containing a list of SIP extensions into a list of extensions that this library
        /// understands.
        /// </summary>
        /// <param name="extensionList">The string containing the list of extensions to parse.</param>
        /// <param name="unknownExtensions">A comma separated list of the unsupported extensions.</param>
        /// <returns>A list of extensions that were understood and a boolean indicating whether any unknown extensions were present.</returns>
        public static List<SIPExtensions> ParseSIPExtensions(string extensionList, out string unknownExtensions)
        {
            List<SIPExtensions> knownExtensions = new List<SIPExtensions>();
            unknownExtensions = null;

            if (String.IsNullOrEmpty(extensionList) == false)
            {
                string[] extensions = extensionList.Trim().Split(',');

                foreach (string extension in extensions)
                {
                    if (String.IsNullOrEmpty(extension) == false)
                    {
                        string trimmedExtension = extension.Trim().ToLower();
                        switch (trimmedExtension)
                        {
                            case PRACK:
                                knownExtensions.Add(SIPExtensions.Prack);
                                break;
                            case NO_REFER_SUB:
                                knownExtensions.Add(SIPExtensions.NoReferSub);
                                break;
                            case REPLACES:
                                knownExtensions.Add(SIPExtensions.Replaces);
                                break;
                            case SIPREC:
                                knownExtensions.Add(SIPExtensions.SipRec);
                                break;
                            case MULTIPLE_REFER:
                                knownExtensions.Add(SIPExtensions.MultipleRefer);
                                break;
                            default:
                                unknownExtensions += (unknownExtensions != null) ? $",{extension.Trim()}" : extension.Trim();
                                break;
                        }
                    }
                }
            }

            return knownExtensions;
        }
    }
}