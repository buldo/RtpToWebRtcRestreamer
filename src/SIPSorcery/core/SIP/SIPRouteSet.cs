using System.Collections.Generic;
using System.Net;

namespace SIPSorcery.SIP
{
    public class SIPRouteSet
    {
        private List<SIPRoute> m_sipRoutes = new List<SIPRoute>();

        public int Length
        {
            get { return m_sipRoutes.Count; }
        }

        public static SIPRouteSet ParseSIPRouteSet(string routeSet)
        {
            SIPRouteSet sipRouteSet = new SIPRouteSet();

            string[] routes = SIPParameters.GetKeyValuePairsFromQuoted(routeSet, ',');
            foreach (string route in routes)
            {
                SIPRoute sipRoute = SIPRoute.ParseSIPRoute(route);
                sipRouteSet.AddBottomRoute(sipRoute);
            }

            return sipRouteSet;
        }

        public SIPRoute GetAt(int index)
        {
            return m_sipRoutes[index];
        }

        public void SetAt(int index, SIPRoute sipRoute)
        {
            m_sipRoutes[index] = sipRoute;
        }

        public SIPRoute TopRoute
        {
            get
            {
                if (m_sipRoutes != null && m_sipRoutes.Count > 0)
                {
                    return m_sipRoutes[0];
                }
                else
                {
                    return null;
                }
            }
        }

        public SIPRoute BottomRoute
        {
            get
            {
                if (m_sipRoutes != null && m_sipRoutes.Count > 0)
                {
                    return m_sipRoutes[m_sipRoutes.Count - 1];
                }
                else
                {
                    return null;
                }
            }
        }

        public void PushRoute(SIPRoute route)
        {
            m_sipRoutes.Insert(0, route);
        }

        public void PushRoute(string host)
        {
            m_sipRoutes.Insert(0, new SIPRoute(host, true));
        }

        public void PushRoute(IPEndPoint socket, SIPSchemesEnum scheme, SIPProtocolsEnum protcol)
        {
            m_sipRoutes.Insert(0, new SIPRoute(scheme + ":" + socket.ToString(), true));
        }

        public void AddBottomRoute(SIPRoute route)
        {
            m_sipRoutes.Insert(m_sipRoutes.Count, route);
        }

        public SIPRoute PopRoute()
        {
            SIPRoute route = null;

            if (m_sipRoutes.Count > 0)
            {
                route = m_sipRoutes[0];
                m_sipRoutes.RemoveAt(0);
            }

            return route;
        }

        public void RemoveBottomRoute()
        {
            if (m_sipRoutes.Count > 0)
            {
                m_sipRoutes.RemoveAt(m_sipRoutes.Count - 1);
            };
        }

        public SIPRouteSet Reversed()
        {
            if (m_sipRoutes != null && m_sipRoutes.Count > 0)
            {
                SIPRouteSet reversedSet = new SIPRouteSet();

                for (int index = 0; index < m_sipRoutes.Count; index++)
                {
                    reversedSet.PushRoute(m_sipRoutes[index]);
                }

                return reversedSet;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// If a route set is travelling from the public side of a proxy to the private side it can be required that the Record-Route set is modified.
        /// </summary>
        /// <param name="origSocket">The socket string in the original route set that needs to be replace.</param>
        /// <param name="replacementSocket">The socket string the original route is being replaced with.</param>
        public void ReplaceRoute(string origSocket, string replacementSocket)
        {
            foreach (SIPRoute route in m_sipRoutes)
            {
                if (route.Host == origSocket)
                {
                    route.Host = replacementSocket;
                }
            }
        }

        public new string ToString()
        {
            string routeStr = null;

            if (m_sipRoutes != null && m_sipRoutes.Count > 0)
            {
                for (int routeIndex = 0; routeIndex < m_sipRoutes.Count; routeIndex++)
                {
                    routeStr += (routeStr != null) ? "," + m_sipRoutes[routeIndex].ToString() : m_sipRoutes[routeIndex].ToString();
                }
            }

            return routeStr;
        }
    }
}