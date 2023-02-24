using System;
using System.Collections.Generic;
using System.Net;
using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    /// <summary>
    /// The Via header only has parameters, no headers. Parameters of from ...;name=value;name2=value2
    /// Specific parameters: ttl, maddr, received, branch.
    /// 
    /// From page 179 of RFC3261:
    /// "Even though this specification mandates that the branch parameter be
    /// present in all requests, the BNF for the header field indicates that
    /// it is optional."
    /// 
    /// The branch parameter on a Via therefore appears to be optionally mandatory?!
    ///
    /// Any SIP application element that uses transactions depends on the branch parameter for transaction matching.
    /// Only the top Via header branch is used for transactions though so if the request has made it to this stack
    /// with missing branches then in theory it should be safe to proceed. It will be left up to the SIPTransaction
    /// class to reject any SIP requests that are missing the necessary branch.
    /// </summary>
    public class SIPViaHeader
    {
        private static char m_paramDelimChar = ';';
        private static char m_hostDelimChar = ':';

        private static string m_receivedKey = SIPHeaderAncillary.SIP_HEADERANC_RECEIVED;
        private static string m_rportKey = SIPHeaderAncillary.SIP_HEADERANC_RPORT;
        private static string m_branchKey = SIPHeaderAncillary.SIP_HEADERANC_BRANCH;

        /// <summary>
        /// Special SIP Via header that is recognised by the SIP transport classes Send methods. At send time this header will be replaced by 
        /// one with IP end point details that reflect the socket the request or response was sent from.
        /// </summary>
        public static SIPViaHeader GetDefaultSIPViaHeader(SIPProtocolsEnum protocol = SIPProtocolsEnum.udp)
        {
            return new SIPViaHeader(new IPEndPoint(IPAddress.Any, 0), CallProperties.CreateBranchId(), protocol);
        }

        public string Version;
        public SIPProtocolsEnum Transport;
        public string Host;
        public int Port = 0;
        public string Branch
        {
            get
            {
                if (ViaParameters != null && ViaParameters.Has(m_branchKey))
                {
                    return ViaParameters.Get(m_branchKey);
                }
                else
                {
                    return null;
                }
            }
            set { ViaParameters.Set(m_branchKey, value); }
        }
        public string ReceivedFromIPAddress     // IP Address contained in the received parameter.
        {
            get
            {
                if (ViaParameters != null && ViaParameters.Has(m_receivedKey))
                {
                    return ViaParameters.Get(m_receivedKey);
                }
                else
                {
                    return null;
                }
            }
            set { ViaParameters.Set(m_receivedKey, value); }
        }
        public int ReceivedFromPort             // Port contained in the rport parameter.
        {
            get
            {
                if (ViaParameters != null && ViaParameters.Has(m_rportKey))
                {
                    return Convert.ToInt32(ViaParameters.Get(m_rportKey));
                }
                else
                {
                    return 0;
                }
            }
            set { ViaParameters.Set(m_rportKey, value.ToString()); }
        }

        public SIPParameters ViaParameters = new SIPParameters(null, m_paramDelimChar);

        public string ContactAddress            // This the address placed into the Via header by the User Agent.
        {
            get
            {
                if (IPSocket.TryParseIPEndPoint(Host, out var ipEndPoint))
                {
                    if (ipEndPoint.Port == 0)
                    {
                        if (Port != 0)
                        {
                            ipEndPoint.Port = Port;
                            return ipEndPoint.ToString();
                        }
                        else
                        {
                            if (ipEndPoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                return "[" + ipEndPoint.Address.ToString() + "]";
                            }
                            else
                            {
                                return ipEndPoint.Address.ToString();
                            }
                        }
                    }
                    else
                    {
                        return ipEndPoint.ToString();
                    }
                }
                else if (Port != 0)
                {
                    return Host + ":" + Port;
                }
                else
                {
                    return Host;
                }
            }
        }

        public string ReceivedFromAddress       // This is the socket the request was received on and is a combination of the Host and Received fields.
        {
            get
            {
                if (ReceivedFromIPAddress != null && ReceivedFromPort != 0)
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ReceivedFromIPAddress), ReceivedFromPort);
                    return ep.ToString();
                }
                else if (ReceivedFromIPAddress != null && Port != 0)
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ReceivedFromIPAddress), Port);
                    return ep.ToString();
                }
                else if (ReceivedFromIPAddress != null)
                {
                    return ReceivedFromIPAddress;
                }
                else if (ReceivedFromPort != 0)
                {
                    if (IPAddress.TryParse(Host, out IPAddress hostip))
                    {
                        IPEndPoint ep = new IPEndPoint(hostip, ReceivedFromPort);
                        return ep.ToString();
                    }
                    else
                    {
                        return Host + ":" + ReceivedFromPort;
                    }
                }
                else if (Port != 0)
                {
                    if (IPAddress.TryParse(Host, out IPAddress hostip))
                    {
                        IPEndPoint ep = new IPEndPoint(hostip, Port);
                        return ep.ToString();
                    }
                    else
                    {
                        return Host + ":" + Port;
                    }
                }
                else
                {
                    return Host;
                }
            }
        }

        public SIPViaHeader()
        { }

        public SIPViaHeader(string contactIPAddress, int contactPort, string branch, SIPProtocolsEnum protocol)
        {
            Version = SIPConstants.SIP_FULLVERSION_STRING;
            Transport = protocol;
            Host = contactIPAddress;
            Port = contactPort;
            Branch = branch;
            ViaParameters.Set(m_rportKey, null);
        }

        public SIPViaHeader(string contactIPAddress, int contactPort, string branch) :
            this(contactIPAddress, contactPort, branch, SIPProtocolsEnum.udp)
        { }

        public SIPViaHeader(IPEndPoint contactEndPoint, string branch) :
            this(contactEndPoint.Address.ToString(), contactEndPoint.Port, branch, SIPProtocolsEnum.udp)
        { }

        public SIPViaHeader(SIPEndPoint localEndPoint, string branch) :
            this(localEndPoint.GetIPEndPoint(), branch, localEndPoint.Protocol)
        { }

        public SIPViaHeader(string contactEndPoint, string branch) :
            this(IPSocket.ParseSocketString(contactEndPoint), branch, SIPProtocolsEnum.udp)
        { }

        public SIPViaHeader(IPEndPoint contactEndPoint, string branch, SIPProtocolsEnum protocol) :
            this(contactEndPoint.Address.ToString(), contactEndPoint.Port, branch, protocol)
        { }

        public static SIPViaHeader[] ParseSIPViaHeader(string viaHeaderStr)
        {
            List<SIPViaHeader> viaHeadersList = new List<SIPViaHeader>();

            if (!viaHeaderStr.IsNullOrBlank())
            {
                viaHeaderStr = viaHeaderStr.Trim();

                // Multiple Via headers can be contained in a single line by separating them with a comma.
                string[] viaHeaders = SIPParameters.GetKeyValuePairsFromQuoted(viaHeaderStr, ',');

                foreach (string viaHeaderStrItem in viaHeaders)
                {
                    if (viaHeaderStrItem == null || viaHeaderStrItem.Trim().Length == 0)
                    {
                        throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "No Contact address.");
                    }
                    else
                    {
                        SIPViaHeader viaHeader = new SIPViaHeader();
                        string header = viaHeaderStrItem.Trim();

                        int firstSpacePosn = header.IndexOf(" ");
                        if (firstSpacePosn == -1)
                        {
                            throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "No Contact address.");
                        }
                        else
                        {
                            string versionAndTransport = header.Substring(0, firstSpacePosn);
                            viaHeader.Version = versionAndTransport.Substring(0, versionAndTransport.LastIndexOf('/'));
                            viaHeader.Transport = SIPProtocolsType.GetProtocolType(versionAndTransport.Substring(versionAndTransport.LastIndexOf('/') + 1));

                            string nextField = header.Substring(firstSpacePosn, header.Length - firstSpacePosn).Trim();

                            int delimIndex = nextField.IndexOf(';');
                            string contactAddress = null;

                            // Some user agents include branch but have the semi-colon missing, that's easy to cope with by replacing "branch" with ";branch".
                            if (delimIndex == -1 && nextField.Contains(m_branchKey))
                            {
                                nextField = nextField.Replace(m_branchKey, ";" + m_branchKey);
                                delimIndex = nextField.IndexOf(';');
                            }

                            if (delimIndex == -1)
                            {
                                //logger.LogWarning("Via header missing semi-colon: " + header + ".");
                                //parserError = SIPValidationError.NoBranchOnVia;
                                //return null;
                                contactAddress = nextField.Trim();
                            }
                            else
                            {
                                contactAddress = nextField.Substring(0, delimIndex).Trim();
                                viaHeader.ViaParameters = new SIPParameters(nextField.Substring(delimIndex, nextField.Length - delimIndex), m_paramDelimChar);
                            }

                            if (contactAddress == null || contactAddress.Trim().Length == 0)
                            {
                                // Check that the branch parameter is present, without it the Via header is illegal.
                                //if (!viaHeader.ViaParameters.Has(m_branchKey))
                                //{
                                //    logger.LogWarning("Via header missing branch: " + header + ".");
                                //    parserError = SIPValidationError.NoBranchOnVia;
                                //    return null;
                                //}

                                throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "No Contact address.");
                            }

                            // Parse the contact address.
                            if (IPSocket.TryParseIPEndPoint(contactAddress, out var ipEndPoint))
                            {
                                viaHeader.Host = ipEndPoint.Address.ToString();
                                if (ipEndPoint.Port != 0)
                                {
                                    viaHeader.Port = ipEndPoint.Port;
                                }
                            }
                            else
                            {
                                // Now parsing non IP address contact addresses.
                                int colonIndex = contactAddress.IndexOf(m_hostDelimChar);
                                if (colonIndex != -1)
                                {
                                    viaHeader.Host = contactAddress.Substring(0, colonIndex);

                                    if (!Int32.TryParse(contactAddress.Substring(colonIndex + 1), out viaHeader.Port))
                                    {
                                        throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "Non-numeric port for IP address.");
                                    }
                                    else if (viaHeader.Port > IPEndPoint.MaxPort)
                                    {
                                        throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "The port specified in a Via header exceeded the maximum allowed.");
                                    }
                                }
                                else
                                {
                                    viaHeader.Host = contactAddress;
                                }
                            }

                            viaHeadersList.Add(viaHeader);
                        }
                    }
                }
            }

            if (viaHeadersList.Count > 0)
            {
                return viaHeadersList.ToArray();
            }
            else
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "Via list was empty.");
            }
        }

        public new string ToString()
        {
            string sipViaHeader = SIPHeaders.SIP_HEADER_VIA + ": " + this.Version + "/" + this.Transport.ToString().ToUpper() + " " + ContactAddress;
            sipViaHeader += (ViaParameters != null && ViaParameters.Count > 0) ? ViaParameters.ToString() : null;

            return sipViaHeader;
        }
    }
}