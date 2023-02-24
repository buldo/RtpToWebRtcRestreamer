using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;

namespace SIPSorcery.SIP
{
    /// <bnf>
    /// Contact        =  ("Contact" / "m" ) HCOLON ( STAR / (contact-param *(COMMA contact-param)))
    /// contact-param  =  (name-addr / addr-spec) *(SEMI contact-params)
    /// name-addr      =  [ display-name ] LAQUOT addr-spec RAQUOT
    /// addr-spec      =  SIP-URI / SIPS-URI / absoluteURI
    /// display-name   =  *(token LWS)/ quoted-string
    ///
    /// contact-params     =  c-p-q / c-p-expires / contact-extension
    /// c-p-q              =  "q" EQUAL qvalue
    /// c-p-expires        =  "expires" EQUAL delta-seconds
    /// contact-extension  =  generic-param
    /// delta-seconds      =  1*DIGIT
    /// generic-param  =  token [ EQUAL gen-value ]
    /// gen-value      =  token / host / quoted-string
    /// </bnf>
    /// <remarks>
    /// The Contact header only has parameters, no headers. Parameters of from ...;name=value;name2=value2
    /// Specific parameters: q, expires.
    /// </remarks>
    [DataContract]
    public class SIPContactHeader
    {
        public const string EXPIRES_PARAMETER_KEY = "expires";
        public const string QVALUE_PARAMETER_KEY = "q";

        //private static char[] m_nonStandardURIDelimChars = new char[] { '\n', '\r', ' ' };	// Characters that can delimit a SIP URI, supposed to be > but it is sometimes missing.

        /// <summary>
        /// Special SIP contact header that is recognised by the SIP transport classes Send methods. At send time this header will be replaced by 
        /// one with IP end point details that reflect the socket the request or response was sent from.
        /// </summary>
        public static SIPContactHeader GetDefaultSIPContactHeader(SIPSchemesEnum scheme)
        {
            return new SIPContactHeader(null, new SIPURI(scheme, IPAddress.Any, 0));
        }

        public string RawHeader;

        public string ContactName
        {
            get { return m_userField.Name; }
            set { m_userField.Name = value; }
        }

        public SIPURI ContactURI
        {
            get { return m_userField.URI; }
            set { m_userField.URI = value; }
        }

        public SIPParameters ContactParameters
        {
            get { return m_userField.Parameters; }
            set { m_userField.Parameters = value; }
        }

        // A value of -1 indicates the header did not contain an expires parameter setting.
        public long Expires
        {
            get
            {
                long expires = -1;

                if (ContactParameters.Has(EXPIRES_PARAMETER_KEY))
                {
                    string expiresStr = ContactParameters.Get(EXPIRES_PARAMETER_KEY);
                    Int64.TryParse(expiresStr, out expires);
                    if (expires > UInt32.MaxValue)
                    {
                        expires = UInt32.MaxValue;
                    }
                }

                return expires;
            }
            set { ContactParameters.Set(EXPIRES_PARAMETER_KEY, value.ToString()); }
        }
        public string Q
        {
            get { return ContactParameters.Get(QVALUE_PARAMETER_KEY); }
            set { ContactParameters.Set(QVALUE_PARAMETER_KEY, value); }
        }

        private SIPUserField m_userField;

        private SIPContactHeader()
        { }

        public SIPContactHeader(string contactName, SIPURI contactURI)
        {
            m_userField = new SIPUserField(contactName, contactURI, null);
        }

        public SIPContactHeader(SIPUserField contactUserField)
        {
            m_userField = contactUserField;
        }

        public static List<SIPContactHeader> ParseContactHeader(string contactHeaderStr)
        {
            try
            {
                if (contactHeaderStr == null || contactHeaderStr.Trim().Length == 0)
                {
                    return null;
                }

                //string[] contactHeaders = null;

                //// Broken User Agent fix (Aastra looking at you!)
                //if (contactHeaderStr.IndexOf('<') != -1 && contactHeaderStr.IndexOf('>') == -1)
                //{
                //    int nonStandardDelimPosn = contactHeaderStr.IndexOfAny(m_nonStandardURIDelimChars);

                //    if (nonStandardDelimPosn != -1)
                //    {
                //        // Add on the missing RQUOT and ignore whatever the rest of the header is.
                //        contactHeaders = new string[] { contactHeaderStr.Substring(0, nonStandardDelimPosn) + ">" };
                //    }
                //    else
                //    {
                //        // Can't work out what is going on with this header bomb out.
                //        throw new SIPValidationException(SIPValidationFieldsEnum.ContactHeader, "Contact header invalid.");
                //    }
                //}
                //else
                //{
                //    contactHeaders = SIPParameters.GetKeyValuePairsFromQuoted(contactHeaderStr, ',');
                //}

                string[] contactHeaders = SIPParameters.GetKeyValuePairsFromQuoted(contactHeaderStr, ',');

                List<SIPContactHeader> contactHeaderList = new List<SIPContactHeader>();

                foreach (string contactHeaderItemStr in contactHeaders)
                {
                    SIPContactHeader contactHeader = new SIPContactHeader();
                    contactHeader.RawHeader = contactHeaderStr;
                    contactHeader.m_userField = SIPUserField.ParseSIPUserField(contactHeaderItemStr);
                    contactHeaderList.Add(contactHeader);
                }

                return contactHeaderList;
            }
            catch (SIPValidationException)
            {
                throw;
            }
            catch (Exception excp)
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.ContactHeader, "Contact header invalid, parse failed. " + excp.Message);
            }
        }

        public static List<SIPContactHeader> CreateSIPContactList(SIPURI sipURI)
        {
            List<SIPContactHeader> contactHeaderList = new List<SIPContactHeader>();
            contactHeaderList.Add(new SIPContactHeader(null, sipURI));

            return contactHeaderList;
        }

        /// <summary>
        /// Compares two contact headers to determine contact address equality.
        /// </summary>
        public static bool AreEqual(SIPContactHeader contact1, SIPContactHeader contact2)
        {
            if (!SIPURI.AreEqual(contact1.ContactURI, contact2.ContactURI))
            {
                return false;
            }
            else
            {
                // Compare invariant parameters.
                string[] contact1Keys = contact1.ContactParameters.GetKeys();

                if (contact1Keys != null && contact1Keys.Length > 0)
                {
                    foreach (string key in contact1Keys)
                    {
                        if (key == EXPIRES_PARAMETER_KEY || key == QVALUE_PARAMETER_KEY)
                        {
                            continue;
                        }
                        else if (contact1.ContactParameters.Get(key) != contact2.ContactParameters.Get(key))
                        {
                            return false;
                        }
                    }
                }

                // Need to do the reverse as well
                string[] contact2Keys = contact2.ContactParameters.GetKeys();

                if (contact2Keys != null && contact2Keys.Length > 0)
                {
                    foreach (string key in contact2Keys)
                    {
                        if (key == EXPIRES_PARAMETER_KEY || key == QVALUE_PARAMETER_KEY)
                        {
                            continue;
                        }
                        else if (contact2.ContactParameters.Get(key) != contact1.ContactParameters.Get(key))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public override string ToString()
        {
            if (m_userField.URI.Host == SIPConstants.SIP_REGISTER_REMOVEALL)
            {
                return SIPConstants.SIP_REGISTER_REMOVEALL;
            }
            else
            {
                //if (m_userField.URI.Protocol == SIPProtocolsEnum.UDP)
                //{
                return m_userField.ToString();
                //}
                //else
                //{
                //    return m_userField.ToContactString();
                //}
            }
        }

        public SIPContactHeader CopyOf()
        {
            SIPContactHeader copy = new SIPContactHeader();
            copy.RawHeader = RawHeader;
            copy.m_userField = m_userField.CopyOf();

            return copy;
        }
    }
}