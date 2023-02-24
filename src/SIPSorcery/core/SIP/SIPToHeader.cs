using System;

namespace SIPSorcery.SIP
{
    /// <bnf>
    /// To				=  ( "To" / "t" ) HCOLON ( name-addr / addr-spec ) *( SEMI to-param )
    /// to-param		=  tag-param / generic-param
    /// name-addr		=  [ display-name ] LAQUOT addr-spec RAQUOT
    /// addr-spec		=  SIP-URI / SIPS-URI / absoluteURI
    /// tag-param       =  "tag" EQUAL token
    /// generic-param   =  token [ EQUAL gen-value ]
    /// gen-value       =  token / host / quoted-string
    /// </bnf>
    /// <remarks>
    /// The To header only has parameters, no headers. Parameters of from ...;name=value;name2=value2.
    /// Specific parameters: tag.
    /// </remarks>
    public class SIPToHeader
    {
        public const string PARAMETER_TAG = SIPHeaderAncillary.SIP_HEADERANC_TAG;

        public string ToName
        {
            get { return m_userField.Name; }
            set { m_userField.Name = value; }
        }

        public SIPURI ToURI
        {
            get { return m_userField.URI; }
            set { m_userField.URI = value; }
        }

        public string ToTag
        {
            get { return ToParameters.Get(PARAMETER_TAG); }
            set
            {
                if (value != null && value.Trim().Length > 0)
                {
                    ToParameters.Set(PARAMETER_TAG, value);
                }
                else
                {
                    if (ToParameters.Has(PARAMETER_TAG))
                    {
                        ToParameters.Remove(PARAMETER_TAG);
                    }
                }
            }
        }

        public SIPParameters ToParameters
        {
            get { return m_userField.Parameters; }
            set { m_userField.Parameters = value; }
        }

        private SIPUserField m_userField;
        public SIPUserField ToUserField
        {
            get { return m_userField; }
            set { m_userField = value; }
        }

        private SIPToHeader()
        { }

        public SIPToHeader(string toName, SIPURI toURI, string toTag)
        {
            m_userField = new SIPUserField(toName, toURI, null);
            ToTag = toTag;
        }

        public static SIPToHeader ParseToHeader(string toHeaderStr)
        {
            try
            {
                SIPToHeader toHeader = new SIPToHeader();

                toHeader.m_userField = SIPUserField.ParseSIPUserField(toHeaderStr);

                return toHeader;
            }
            catch (ArgumentException argExcp)
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.ToHeader, argExcp.Message);
            }
            catch
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.ToHeader, "The SIP To header was invalid.");
            }
        }

        public override string ToString()
        {
            return m_userField.ToString();
        }
    }
}