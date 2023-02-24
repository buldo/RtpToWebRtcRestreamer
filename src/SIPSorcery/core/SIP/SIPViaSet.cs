using System.Collections.Generic;
using System.Net;

namespace SIPSorcery.SIP
{
    public class SIPViaSet
    {
        private static string m_CRLF = SIPConstants.CRLF;

        private List<SIPViaHeader> m_viaHeaders = new List<SIPViaHeader>();

        public int Length
        {
            get { return m_viaHeaders.Count; }
        }

        public List<SIPViaHeader> Via
        {
            get
            {
                return m_viaHeaders;
            }
            set
            {
                m_viaHeaders = value;
            }
        }

        public SIPViaHeader TopViaHeader
        {
            get
            {
                if (m_viaHeaders != null && m_viaHeaders.Count > 0)
                {
                    return m_viaHeaders[0];
                }
                else
                {
                    return null;
                }
            }
        }

        public SIPViaHeader BottomViaHeader
        {
            get
            {
                if (m_viaHeaders != null && m_viaHeaders.Count > 0)
                {
                    return m_viaHeaders[m_viaHeaders.Count - 1];
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Pops top Via header off the array.
        /// </summary>
        public SIPViaHeader PopTopViaHeader()
        {
            SIPViaHeader topHeader = m_viaHeaders[0];
            m_viaHeaders.RemoveAt(0);

            return topHeader;
        }

        public void AddBottomViaHeader(SIPViaHeader viaHeader)
        {
            m_viaHeaders.Add(viaHeader);
        }

        /// <summary>
        /// Updates the topmost Via header by setting the received and rport parameters to the IP address and port
        /// the request came from.
        /// </summary>
        /// <remarks>The setting of the received parameter is documented in RFC3261 section 18.2.1 and in RFC3581
        /// section 4. RFC3581 states that the received parameter value must be set even if it's the same as the 
        /// address in the sent from field. The setting of the rport parameter is documented in RFC3581 section 4.
        /// An attempt was made to comply with the RFC3581 standard and only set the rport parameter if it was included
        /// by the client user agent however in the wild there are too many user agents that are behind symmetric NATs 
        /// not setting an empty rport and if it's not added then they will not be able to communicate.
        /// </remarks>
        /// <param name="msgRcvdEndPoint">The remote endpoint the request was received from.</param>
        public void UpateTopViaHeader(IPEndPoint msgRcvdEndPoint)
        {
            // Update the IP Address and port that this request was received on.
            SIPViaHeader topViaHeader = this.TopViaHeader;

            topViaHeader.ReceivedFromIPAddress = msgRcvdEndPoint.Address.ToString();
            topViaHeader.ReceivedFromPort = msgRcvdEndPoint.Port;
        }

        /// <summary>
        /// Pushes a new Via header onto the top of the array.
        /// </summary>
        public void PushViaHeader(SIPViaHeader viaHeader)
        {
            m_viaHeaders.Insert(0, viaHeader);
        }

        public new string ToString()
        {
            string viaStr = null;

            if (m_viaHeaders != null && m_viaHeaders.Count > 0)
            {
                for (int viaIndex = 0; viaIndex < m_viaHeaders.Count; viaIndex++)
                {
                    viaStr += (m_viaHeaders[viaIndex]).ToString() + m_CRLF;
                }
            }

            return viaStr;
        }
    }
}