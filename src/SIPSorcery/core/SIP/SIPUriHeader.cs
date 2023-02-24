using System;
using System.Collections.Generic;
using System.Net;

namespace SIPSorcery.SIP
{
    /// <summary>
    /// Class used to parse History-Info, Diversion, P-Asserted-Identity Headers.
    /// </summary>
    public class SIPUriHeader
    {
        public static SIPUriHeader GetDefaultHeader(SIPSchemesEnum scheme)
        {
            return new SIPUriHeader(null, new SIPURI(scheme, IPAddress.Any, 0));
        }

        public string Name
        {
            get { return m_userField.Name; }
            set { m_userField.Name = value; }
        }

        public SIPURI URI
        {
            get { return m_userField.URI; }
            set { m_userField.URI = value; }
        }

        public SIPParameters Parameters
        {
            get { return m_userField.Parameters; }
            set { m_userField.Parameters = value; }
        }

        private SIPUserField m_userField = new SIPUserField();
        public SIPUserField UserField
        {
            get { return m_userField; }
            set { m_userField = value; }
        }

        public SIPUriHeader()
        { }

        public static bool SortByUriParameter(ref List<SIPUriHeader> header, string paramterForSort, bool descending = false)
        {
            if (header == null)
            {
                return false;
            }

            try
            {
                if (descending)
                {
                    header.Sort((x, y) => y.Parameters.Get(paramterForSort).CompareTo(x.Parameters.Get(paramterForSort)));
                }
                else
                {
                    header.Sort((x, y) => x.Parameters.Get(paramterForSort).CompareTo(y.Parameters.Get(paramterForSort)));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public SIPUriHeader(string Name, SIPURI URI, string uriParams = null)
        {
            m_userField = new SIPUserField(Name, URI, uriParams);
        }
        public static List<SIPUriHeader> ParseHeader(string headerStr)
        {
            try
            {
                var returnHeaders = new List<SIPUriHeader>();

                string[] uris = SIPParameters.GetKeyValuePairsFromQuoted(headerStr, ',');

                foreach (string uri in uris)
                {
                    var NewHeader = new SIPUriHeader();
                    NewHeader.m_userField = SIPUserField.ParseSIPUserField(uri);
                    returnHeaders.Add(NewHeader);
                }

                return returnHeaders;
            }
            catch (ArgumentException argExcp)
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.Unknown, argExcp.Message);
            }
            catch
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.Unknown, "One of the SIP SIPMultiUriHeaders was invalid, header: " + headerStr);
            }
        }

        public override string ToString()
        {
            return m_userField.ToString();
        }

        /// <summary>
        /// Returns a friendly description of the caller that's suitable for humans. Leaves out
        /// all the parameters etc.
        /// </summary>
        /// <returns>A string representing a friendly description of the MultiUri header.</returns>
        public string FriendlyDescription()
        {
            string caller = URI.ToAOR();
            caller = (!string.IsNullOrEmpty(Name)) ? Name + " " + caller : caller;
            return caller;
        }
    }
}