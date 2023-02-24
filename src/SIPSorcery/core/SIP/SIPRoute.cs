using System;
using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    /// <summary>
    /// The SIPRoute class is used to represent both Route and Record-Route headers.
    /// </summary>
    /// <bnf>
    /// Route               =  "Route" HCOLON route-param *(COMMA route-param)
    /// route-param         =  name-addr *( SEMI rr-param )
    /// 
    /// Record-Route        =  "Record-Route" HCOLON rec-route *(COMMA rec-route)
    /// rec-route           =  name-addr *( SEMI rr-param )
    /// rr-param            =  generic-param
    ///
    /// name-addr           =  [ display-name ] LAQUOT addr-spec RAQUOT
    /// addr-spec           =  SIP-URI / SIPS-URI / absoluteURI
    /// display-name        =  *(token LWS)/ quoted-string
    /// generic-param       =  token [ EQUAL gen-value ]
    /// gen-value           =  token / host / quoted-string
    /// </bnf>
    /// <remarks>
    /// The Route and Record-Route headers only have parameters, no headers. Parameters of from ...;name=value;name2=value2
    /// There are no specific parameters.
    /// </remarks>
    public class SIPRoute
    {
        private static string m_looseRouterParameter = SIPConstants.SIP_LOOSEROUTER_PARAMETER;

        private static char[] m_angles = new char[] { '<', '>' };

        private SIPUserField m_userField;

        public string Host
        {
            get { return m_userField.URI.Host; }
            set { m_userField.URI.Host = value; }
        }

        public SIPURI URI
        {
            get { return m_userField.URI; }
        }

        public bool IsStrictRouter
        {
            get { return !m_userField.URI.Parameters.Has(m_looseRouterParameter); }
            set
            {
                if (value)
                {
                    m_userField.URI.Parameters.Remove(m_looseRouterParameter);
                }
                else
                {
                    m_userField.URI.Parameters.Set(m_looseRouterParameter, null);
                }
            }
        }

        private SIPRoute()
        { }

        public SIPRoute(string host)
        {
            if (host.IsNullOrBlank())
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.RouteHeader, "Cannot create a Route from an blank string.");
            }

            m_userField = SIPUserField.ParseSIPUserField(host);
        }

        public SIPRoute(string host, bool looseRouter)
        {
            if (host.IsNullOrBlank())
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.RouteHeader, "Cannot create a Route from an blank string.");
            }

            m_userField = SIPUserField.ParseSIPUserField(host);
            this.IsStrictRouter = !looseRouter;
        }

        public SIPRoute(SIPURI uri)
        {
            m_userField = new SIPUserField();
            m_userField.URI = uri;
        }

        public SIPRoute(SIPURI uri, bool looseRouter)
        {
            m_userField = new SIPUserField();
            m_userField.URI = uri;
            this.IsStrictRouter = !looseRouter;
        }

        public static SIPRoute ParseSIPRoute(string route)
        {
            if (route.IsNullOrBlank())
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.RouteHeader, "Cannot create a Route from an blank string.");
            }

            try
            {
                SIPRoute sipRoute = new SIPRoute();
                sipRoute.m_userField = SIPUserField.ParseSIPUserField(route);

                return sipRoute;
            }
            catch (Exception excp)
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.RouteHeader, excp.Message);
            }
        }

        public override string ToString()
        {
            //if (m_userField.URI.Protocol == SIPProtocolsEnum.UDP)
            //{
            return m_userField.ToString();
            //}
            //else
            //{
            //return m_userField.ToContactString();
            //}
        }

        public SIPEndPoint ToSIPEndPoint()
        {
            return URI.ToSIPEndPoint();
        }
    }
}