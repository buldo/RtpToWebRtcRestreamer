using System;
using System.Net;

namespace SIPSorcery.SIP
{
    /// <bnf>
    /// From            =  ( "From" / "f" ) HCOLON from-spec
    /// from-spec       =  ( name-addr / addr-spec ) *( SEMI from-param )
    /// from-param      =  tag-param / generic-param
    /// name-addr		=  [ display-name ] LAQUOT addr-spec RAQUOT
    /// addr-spec		=  SIP-URI / SIPS-URI / absoluteURI
    /// tag-param       =  "tag" EQUAL token
    /// generic-param   =  token [ EQUAL gen-value ]
    /// gen-value       =  token / host / quoted-string
    /// </bnf>
    /// <remarks>
    /// The From header only has parameters, no headers. Parameters of from ...;name=value;name2=value2.
    /// Specific parameters: tag.
    /// </remarks>
    public class SIPFromHeader
    {
        //public const string DEFAULT_FROM_NAME = SIPConstants.SIP_DEFAULT_USERNAME;
        public const string DEFAULT_FROM_URI = SIPConstants.SIP_DEFAULT_FROMURI;
        public const string PARAMETER_TAG = SIPHeaderAncillary.SIP_HEADERANC_TAG;

        /// <summary>
        /// Special SIP From header that is recognised by the SIP transport classes Send methods. At send time this header will be replaced by 
        /// one with IP end point details that reflect the socket the request or response was sent from.
        /// </summary>
        public static SIPFromHeader GetDefaultSIPFromHeader(SIPSchemesEnum scheme)
        {
            return new SIPFromHeader(null, new SIPURI(scheme, IPAddress.Any, 0), CallProperties.CreateNewTag());
        }

        public string FromName
        {
            get { return m_userField.Name; }
            set { m_userField.Name = value; }
        }

        public SIPURI FromURI
        {
            get { return m_userField.URI; }
            set { m_userField.URI = value; }
        }

        public string FromTag
        {
            get { return FromParameters.Get(PARAMETER_TAG); }
            set
            {
                if (value != null && value.Trim().Length > 0)
                {
                    FromParameters.Set(PARAMETER_TAG, value);
                }
                else
                {
                    if (FromParameters.Has(PARAMETER_TAG))
                    {
                        FromParameters.Remove(PARAMETER_TAG);
                    }
                }
            }
        }

        public SIPParameters FromParameters
        {
            get { return m_userField.Parameters; }
            set { m_userField.Parameters = value; }
        }

        private SIPUserField m_userField = new SIPUserField();
        public SIPUserField FromUserField
        {
            get { return m_userField; }
            set { m_userField = value; }
        }

        private SIPFromHeader()
        { }

        public SIPFromHeader(string fromName, SIPURI fromURI, string fromTag)
        {
            m_userField = new SIPUserField(fromName, fromURI, null);
            FromTag = fromTag;
        }

        public static SIPFromHeader ParseFromHeader(string fromHeaderStr)
        {
            try
            {
                SIPFromHeader fromHeader = new SIPFromHeader();

                fromHeader.m_userField = SIPUserField.ParseSIPUserField(fromHeaderStr);

                return fromHeader;
            }
            catch (ArgumentException argExcp)
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.FromHeader, argExcp.Message);
            }
            catch
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.FromHeader, "The SIP From header was invalid.");
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
        /// <returns>A string representing a friendly description of the From header.</returns>
        public string FriendlyDescription()
        {
            string caller = FromURI.ToAOR();
            caller = (!string.IsNullOrEmpty(FromName)) ? FromName + " " + caller : caller;
            return caller;
        }
    }
}