//-----------------------------------------------------------------------------
// Filename: SIPHeader.cs
//
// Description: SIP Header.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 17 Sep 2005	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;

namespace SIPSorcery.SIP
{
    /// <bnf>
    /// header  =  "header-name" HCOLON header-value *(COMMA header-value)
    /// field-name: field-value CRLF
    /// </bnf>
    public class SIPHeader
    {
        public const int DEFAULT_CSEQ = 100;

        private static ILogger logger = Log.Logger;
        private static string m_CRLF = SIPConstants.CRLF;

        // RFC SIP headers.
        public string Accept;
        public string AcceptEncoding;
        public string AcceptLanguage;
        public string AlertInfo;
        public string Allow;
        public string AllowEvents;                          // RFC3265 SIP Events.
        public string AuthenticationInfo;
        public List<SIPAuthenticationHeader> AuthenticationHeaders = new List<SIPAuthenticationHeader>();
        public string CallId;
        public string CallInfo;
        public List<SIPContactHeader> Contact = new List<SIPContactHeader>();
        public string ContentDisposition;
        public string ContentEncoding;
        public string ContentLanguage;
        public string ContentType;
        public int ContentLength = 0;
        public int CSeq = -1;
        public SIPMethodsEnum CSeqMethod;
        public string Date;
        public string ErrorInfo;
        public string ETag;                                 // added by Tilmann: look RFC3903
        public string Event;                                // RFC3265 SIP Events.
        public long Expires = -1;
        public SIPFromHeader From;
        public string InReplyTo;
        public long MinExpires = -1;
        public int MaxForwards = SIPConstants.DEFAULT_MAX_FORWARDS;
        public string MIMEVersion;
        public string Organization;
        public string Priority;
        public string ProxyRequire;
        public int RAckCSeq = -1;                          // RFC3262 the CSeq number the PRACK request is acknowledging.
        public SIPMethodsEnum RAckCSeqMethod;              // RFC3262 the CSeq method from the response the PRACK request is acknowledging.
        public int RAckRSeq = -1;                          // RFC3262 the RSeq number the PRACK request is acknowledging.
        public string Reason;
        public SIPRouteSet RecordRoutes = new SIPRouteSet();
        public string ReferredBy;                           // RFC 3515 "The Session Initiation Protocol (SIP) Refer Method"
        public string ReferSub;                             // RFC 4488 If set to false indicates the implicit REFER subscription should not be created.
        public string ReferTo;                              // RFC 3515 "The Session Initiation Protocol (SIP) Refer Method"
        public string ReplyTo;
        public string Replaces;
        public string Require;
        public string RetryAfter;
        public int RSeq = -1;                               // RFC3262 reliable provisional response sequence number.
        public SIPRouteSet Routes = new SIPRouteSet();
        public string Server;
        public string Subject;
        public string SubscriptionState;                    // RFC3265 SIP Events.
        public string Supported;
        public string Timestamp;
        public SIPToHeader To;
        public string Unsupported;
        public string UserAgent;
        public SIPViaSet Vias = new SIPViaSet();
        public string Warning;
        public List<SIPUriHeader> PassertedIdentity = new List<SIPUriHeader>();       // RFC3325.

        /// <summary>
        /// It's quaranteed to be sorted from shortest index to longest index.
        /// </summary>
        public List<SIPUriHeader> HistoryInfo = new List<SIPUriHeader>();             // RFC4244.

        public List<SIPUriHeader> Diversion = new List<SIPUriHeader>();               // RFC5806.

        // Non-core custom SIP headers used to allow a SIP Proxy to communicate network info to internal server agents.
        public string ProxyReceivedOn;          // The Proxy socket that the SIP message was received on.
        public string ProxyReceivedFrom;        // The remote socket that the Proxy received the SIP message on.
        public string ProxySendFrom;            // The Proxy socket that the SIP message should be transmitted from.

        public List<string> UnknownHeaders = new List<string>();    // Holds any unrecognised headers.

        public List<SIPExtensions> RequiredExtensions = new List<SIPExtensions>();
        public string UnknownRequireExtension = null;
        public List<SIPExtensions> SupportedExtensions = new List<SIPExtensions>();

        public bool HasAuthenticationHeader => AuthenticationHeaders.Count > 0;

        public SIPHeader()
        { }

        public SIPHeader(string fromHeader, string toHeader, int cseq, string callId)
        {
            SIPFromHeader from = SIPFromHeader.ParseFromHeader(fromHeader);
            SIPToHeader to = SIPToHeader.ParseToHeader(toHeader);
            Initialise(null, from, to, cseq, callId);
        }

        public SIPHeader(string fromHeader, string toHeader, string contactHeader, int cseq, string callId)
        {
            SIPFromHeader from = SIPFromHeader.ParseFromHeader(fromHeader);
            SIPToHeader to = SIPToHeader.ParseToHeader(toHeader);
            List<SIPContactHeader> contact = SIPContactHeader.ParseContactHeader(contactHeader);
            Initialise(contact, from, to, cseq, callId);
        }

        public SIPHeader(SIPFromHeader from, SIPToHeader to, int cseq, string callId)
        {
            Initialise(null, from, to, cseq, callId);
        }

        public SIPHeader(SIPContactHeader contact, SIPFromHeader from, SIPToHeader to, int cseq, string callId)
        {
            List<SIPContactHeader> contactList = new List<SIPContactHeader>();
            if (contact != null)
            {
                contactList.Add(contact);
            }

            Initialise(contactList, from, to, cseq, callId);
        }

        public SIPHeader(List<SIPContactHeader> contactList, SIPFromHeader from, SIPToHeader to, int cseq, string callId)
        {
            Initialise(contactList, from, to, cseq, callId);
        }

        private void Initialise(List<SIPContactHeader> contact, SIPFromHeader from, SIPToHeader to, int cseq, string callId)
        {
            if (from == null)
            {
                throw new ApplicationException("The From header cannot be empty when creating a new SIP header.");
            }

            if (to == null)
            {
                throw new ApplicationException("The To header cannot be empty when creating a new SIP header.");
            }

            if (callId == null || callId.Trim().Length == 0)
            {
                throw new ApplicationException("The CallId header cannot be empty when creating a new SIP header.");
            }

            From = from;
            To = to;
            Contact = contact;
            CallId = callId;

            if (cseq >= 0 && cseq < Int32.MaxValue)
            {
                CSeq = cseq;
            }
            else
            {
                CSeq = DEFAULT_CSEQ;
            }
        }

        public static string[] SplitHeaders(string message)
        {
            // SIP headers can be extended across lines if the first character of the next line is at least on whitespace character.
            message = Regex.Replace(message, m_CRLF + @"\s+", " ", RegexOptions.Singleline);

            // Some user agents couldn't get the \r\n bit right.
            message = Regex.Replace(message, "\r ", m_CRLF, RegexOptions.Singleline);

            return Regex.Split(message, m_CRLF);
        }

        public static SIPHeader ParseSIPHeaders(string[] headersCollection)
        {
            try
            {
                SIPHeader sipHeader = new SIPHeader();
                sipHeader.MaxForwards = -1;     // This allows detection of whether this header is present or not.
                string lastHeader = null;

                for (int lineIndex = 0; lineIndex < headersCollection.Length; lineIndex++)
                {
                    string headerLine = headersCollection[lineIndex];

                    if (headerLine.IsNullOrBlank())
                    {
                        // No point processing blank headers.
                        continue;
                    }

                    string headerName = null;
                    string headerValue = null;

                    // If the first character of a line is whitespace it's a continuation of the previous line.
                    if (headerLine.StartsWith(" "))
                    {
                        headerName = lastHeader;
                        headerValue = headerLine.Trim();
                    }
                    else
                    {
                        headerLine = headerLine.Trim();
                        int delimiterIndex = headerLine.IndexOf(SIPConstants.HEADER_DELIMITER_CHAR);

                        if (delimiterIndex == -1)
                        {
                            logger.LogWarning("Invalid SIP header, ignoring, " + headerLine + ".");
                            continue;
                        }

                        headerName = headerLine.Substring(0, delimiterIndex).Trim();
                        headerValue = headerLine.Substring(delimiterIndex + 1).Trim();
                    }

                    try
                    {
                        string headerNameLower = headerName.ToLower();

                        #region Via
                        if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_VIA ||
                            headerNameLower == SIPHeaders.SIP_HEADER_VIA.ToLower())
                        {
                            //sipHeader.RawVia += headerValue;

                            SIPViaHeader[] viaHeaders = SIPViaHeader.ParseSIPViaHeader(headerValue);

                            if (viaHeaders != null && viaHeaders.Length > 0)
                            {
                                foreach (SIPViaHeader viaHeader in viaHeaders)
                                {
                                    sipHeader.Vias.AddBottomViaHeader(viaHeader);
                                }
                            }
                        }
                        #endregion
                        #region CallId
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_CALLID ||
                                headerNameLower == SIPHeaders.SIP_HEADER_CALLID.ToLower())
                        {
                            sipHeader.CallId = headerValue;
                        }
                        #endregion
                        #region CSeq
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_CSEQ.ToLower())
                        {
                            //sipHeader.RawCSeq += headerValue;

                            string[] cseqFields = headerValue.Split(' ');
                            if (cseqFields == null || cseqFields.Length == 0)
                            {
                                logger.LogWarning("The " + SIPHeaders.SIP_HEADER_CSEQ + " was empty.");
                            }
                            else
                            {
                                if (!Int32.TryParse(cseqFields[0], out sipHeader.CSeq))
                                {
                                    logger.LogWarning(SIPHeaders.SIP_HEADER_CSEQ + " did not contain a valid integer, " + headerLine + ".");
                                }

                                if (cseqFields != null && cseqFields.Length > 1)
                                {
                                    sipHeader.CSeqMethod = SIPMethods.GetMethod(cseqFields[1]);
                                }
                                else
                                {
                                    logger.LogWarning("There was no " + SIPHeaders.SIP_HEADER_CSEQ + " method, " + headerLine + ".");
                                }
                            }
                        }
                        #endregion
                        #region Expires
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_EXPIRES.ToLower())
                        {
                            //sipHeader.RawExpires += headerValue;

                            if (!Int64.TryParse(headerValue, out sipHeader.Expires))
                            {
                                logger.LogWarning("The Expires value was not a valid integer, " + headerLine + ".");
                            }
                        }
                        #endregion
                        #region Min-Expires
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_MINEXPIRES.ToLower())
                        {
                            if (!Int64.TryParse(headerValue, out sipHeader.MinExpires))
                            {
                                logger.LogWarning("The Min-Expires value was not a valid integer, " + headerLine + ".");
                            }
                        }
                        #endregion
                        #region Contact
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_CONTACT ||
                            headerNameLower == SIPHeaders.SIP_HEADER_CONTACT.ToLower())
                        {
                            List<SIPContactHeader> contacts = SIPContactHeader.ParseContactHeader(headerValue);
                            if (contacts != null && contacts.Count > 0)
                            {
                                sipHeader.Contact.AddRange(contacts);
                            }
                        }
                        #endregion
                        #region From
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_FROM ||
                             headerNameLower == SIPHeaders.SIP_HEADER_FROM.ToLower())
                        {
                            //sipHeader.RawFrom = headerValue;
                            sipHeader.From = SIPFromHeader.ParseFromHeader(headerValue);
                        }
                        #endregion
                        #region To
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_TO ||
                            headerNameLower == SIPHeaders.SIP_HEADER_TO.ToLower())
                        {
                            //sipHeader.RawTo = headerValue;
                            sipHeader.To = SIPToHeader.ParseToHeader(headerValue);
                        }
                        #endregion
                        #region WWWAuthenticate
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_WWWAUTHENTICATE.ToLower())
                        {
                            //sipHeader.RawAuthentication = headerValue;
                            sipHeader.AuthenticationHeaders.Add(SIPAuthenticationHeader.ParseSIPAuthenticationHeader(SIPAuthorisationHeadersEnum.WWWAuthenticate, headerValue));
                        }
                        #endregion
                        #region Authorization
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_AUTHORIZATION.ToLower())
                        {
                            //sipHeader.RawAuthentication = headerValue;
                            sipHeader.AuthenticationHeaders.Add(SIPAuthenticationHeader.ParseSIPAuthenticationHeader(SIPAuthorisationHeadersEnum.Authorize, headerValue));
                        }
                        #endregion
                        #region ProxyAuthentication
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PROXYAUTHENTICATION.ToLower())
                        {
                            //sipHeader.RawAuthentication = headerValue;
                            sipHeader.AuthenticationHeaders.Add(SIPAuthenticationHeader.ParseSIPAuthenticationHeader(SIPAuthorisationHeadersEnum.ProxyAuthenticate, headerValue));
                        }
                        #endregion
                        #region ProxyAuthorization
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PROXYAUTHORIZATION.ToLower())
                        {
                            sipHeader.AuthenticationHeaders.Add(SIPAuthenticationHeader.ParseSIPAuthenticationHeader(SIPAuthorisationHeadersEnum.ProxyAuthorization, headerValue));
                        }
                        #endregion
                        #region UserAgent
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_USERAGENT.ToLower())
                        {
                            sipHeader.UserAgent = headerValue;
                        }
                        #endregion
                        #region MaxForwards
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_MAXFORWARDS.ToLower())
                        {
                            if (!Int32.TryParse(headerValue, out sipHeader.MaxForwards))
                            {
                                logger.LogWarning("The " + SIPHeaders.SIP_HEADER_MAXFORWARDS + " could not be parsed as a valid integer, " + headerLine + ".");
                            }
                        }
                        #endregion
                        #region ContentLength
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_CONTENTLENGTH ||
                            headerNameLower == SIPHeaders.SIP_HEADER_CONTENTLENGTH.ToLower())
                        {
                            if (!Int32.TryParse(headerValue, out sipHeader.ContentLength))
                            {
                                logger.LogWarning("The " + SIPHeaders.SIP_HEADER_CONTENTLENGTH + " could not be parsed as a valid integer.");
                            }
                        }
                        #endregion
                        #region ContentType
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_CONTENTTYPE ||
                            headerNameLower == SIPHeaders.SIP_HEADER_CONTENTTYPE.ToLower())
                        {
                            sipHeader.ContentType = headerValue;
                        }
                        #endregion
                        #region Accept
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ACCEPT.ToLower())
                        {
                            sipHeader.Accept = headerValue;
                        }
                        #endregion
                        #region Route
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ROUTE.ToLower())
                        {
                            SIPRouteSet routeSet = SIPRouteSet.ParseSIPRouteSet(headerValue);
                            if (routeSet != null)
                            {
                                while (routeSet.Length > 0)
                                {
                                    sipHeader.Routes.AddBottomRoute(routeSet.PopRoute());
                                }
                            }
                        }
                        #endregion
                        #region RecordRoute
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_RECORDROUTE.ToLower())
                        {
                            SIPRouteSet recordRouteSet = SIPRouteSet.ParseSIPRouteSet(headerValue);
                            if (recordRouteSet != null)
                            {
                                while (recordRouteSet.Length > 0)
                                {
                                    sipHeader.RecordRoutes.AddBottomRoute(recordRouteSet.PopRoute());
                                }
                            }
                        }
                        #endregion
                        #region Allow-Events
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ALLOW_EVENTS || headerNameLower == SIPHeaders.SIP_COMPACTHEADER_ALLOWEVENTS)
                        {
                            sipHeader.AllowEvents = headerValue;
                        }
                        #endregion
                        #region Event
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_EVENT.ToLower() || headerNameLower == SIPHeaders.SIP_COMPACTHEADER_EVENT)
                        {
                            sipHeader.Event = headerValue;
                        }
                        #endregion
                        #region SubscriptionState.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_SUBSCRIPTION_STATE.ToLower())
                        {
                            sipHeader.SubscriptionState = headerValue;
                        }
                        #endregion
                        #region Timestamp.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_TIMESTAMP.ToLower())
                        {
                            sipHeader.Timestamp = headerValue;
                        }
                        #endregion
                        #region Date.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_DATE.ToLower())
                        {
                            sipHeader.Date = headerValue;
                        }
                        #endregion
                        #region Refer-Sub.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REFERSUB.ToLower())
                        {
                            if (sipHeader.ReferSub == null)
                            {
                                sipHeader.ReferSub = headerValue;
                            }
                            else
                            {
                                throw new SIPValidationException(SIPValidationFieldsEnum.ReferToHeader, "Only a single Refer-Sub header is permitted.");
                            }
                        }
                        #endregion
                        #region Refer-To.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REFERTO.ToLower() ||
                            headerNameLower == SIPHeaders.SIP_COMPACTHEADER_REFERTO)
                        {
                            if (sipHeader.ReferTo == null)
                            {
                                sipHeader.ReferTo = headerValue;
                            }
                            else
                            {
                                throw new SIPValidationException(SIPValidationFieldsEnum.ReferToHeader, "Only a single Refer-To header is permitted.");
                            }
                        }
                        #endregion
                        #region Referred-By.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REFERREDBY.ToLower())
                        {
                            sipHeader.ReferredBy = headerValue;
                        }
                        #endregion
                        #region Replaces.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REPLACES.ToLower())
                        {
                            sipHeader.Replaces = headerValue;
                        }
                        #endregion
                        #region Require.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REQUIRE.ToLower())
                        {
                            sipHeader.Require = headerValue;

                            if (!String.IsNullOrEmpty(sipHeader.Require))
                            {
                                sipHeader.RequiredExtensions = SIPExtensionHeaders.ParseSIPExtensions(sipHeader.Require, out sipHeader.UnknownRequireExtension);
                            }
                        }
                        #endregion
                        #region Reason.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REASON.ToLower())
                        {
                            sipHeader.Reason = headerValue;
                        }
                        #endregion
                        #region Proxy-ReceivedFrom.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PROXY_RECEIVEDFROM.ToLower())
                        {
                            sipHeader.ProxyReceivedFrom = headerValue;
                        }
                        #endregion
                        #region Proxy-ReceivedOn.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PROXY_RECEIVEDON.ToLower())
                        {
                            sipHeader.ProxyReceivedOn = headerValue;
                        }
                        #endregion
                        #region Proxy-SendFrom.
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PROXY_SENDFROM.ToLower())
                        {
                            sipHeader.ProxySendFrom = headerValue;
                        }
                        #endregion
                        #region Supported
                        else if (headerNameLower == SIPHeaders.SIP_COMPACTHEADER_SUPPORTED ||
                            headerNameLower == SIPHeaders.SIP_HEADER_SUPPORTED.ToLower())
                        {
                            sipHeader.Supported = headerValue;

                            if (!String.IsNullOrEmpty(sipHeader.Supported))
                            {
                                sipHeader.SupportedExtensions = SIPExtensionHeaders.ParseSIPExtensions(sipHeader.Supported, out _);
                            }
                        }
                        #endregion
                        #region Authentication-Info
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_AUTHENTICATIONINFO.ToLower())
                        {
                            sipHeader.AuthenticationInfo = headerValue;
                        }
                        #endregion
                        #region Accept-Encoding
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ACCEPTENCODING.ToLower())
                        {
                            sipHeader.AcceptEncoding = headerValue;
                        }
                        #endregion
                        #region Accept-Language
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ACCEPTLANGUAGE.ToLower())
                        {
                            sipHeader.AcceptLanguage = headerValue;
                        }
                        #endregion
                        #region Alert-Info
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ALERTINFO.ToLower())
                        {
                            sipHeader.AlertInfo = headerValue;
                        }
                        #endregion
                        #region Allow
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ALLOW.ToLower())
                        {
                            sipHeader.Allow = headerValue;
                        }
                        #endregion
                        #region Call-Info
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_CALLINFO.ToLower())
                        {
                            sipHeader.CallInfo = headerValue;
                        }
                        #endregion
                        #region Content-Disposition
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_CONTENT_DISPOSITION.ToLower())
                        {
                            sipHeader.ContentDisposition = headerValue;
                        }
                        #endregion
                        #region Content-Encoding
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_CONTENT_ENCODING.ToLower())
                        {
                            sipHeader.ContentEncoding = headerValue;
                        }
                        #endregion
                        #region Content-Language
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_CONTENT_LANGUAGE.ToLower())
                        {
                            sipHeader.ContentLanguage = headerValue;
                        }
                        #endregion
                        #region Error-Info
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ERROR_INFO.ToLower())
                        {
                            sipHeader.ErrorInfo = headerValue;
                        }
                        #endregion
                        #region In-Reply-To
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_IN_REPLY_TO.ToLower())
                        {
                            sipHeader.InReplyTo = headerValue;
                        }
                        #endregion
                        #region MIME-Version
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_MIME_VERSION.ToLower())
                        {
                            sipHeader.MIMEVersion = headerValue;
                        }
                        #endregion
                        #region Organization
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ORGANIZATION.ToLower())
                        {
                            sipHeader.Organization = headerValue;
                        }
                        #endregion
                        #region Priority
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PRIORITY.ToLower())
                        {
                            sipHeader.Priority = headerValue;
                        }
                        #endregion
                        #region Proxy-Require
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PROXY_REQUIRE.ToLower())
                        {
                            sipHeader.ProxyRequire = headerValue;
                        }
                        #endregion
                        #region Reply-To
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_REPLY_TO.ToLower())
                        {
                            sipHeader.ReplyTo = headerValue;
                        }
                        #endregion
                        #region Retry-After
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_RETRY_AFTER.ToLower())
                        {
                            sipHeader.RetryAfter = headerValue;
                        }
                        #endregion
                        #region Subject
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_SUBJECT.ToLower())
                        {
                            sipHeader.Subject = headerValue;
                        }
                        #endregion
                        #region Unsupported
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_UNSUPPORTED.ToLower())
                        {
                            sipHeader.Unsupported = headerValue;
                        }
                        #endregion
                        #region Warning
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_WARNING.ToLower())
                        {
                            sipHeader.Warning = headerValue;
                        }
                        #endregion
                        #region ETag
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_ETAG.ToLower())
                        {
                            sipHeader.ETag = headerValue;
                        }
                        #endregion
                        #region RAck
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_RELIABLE_ACK.ToLower())
                        {
                            string[] rackFields = headerValue.Split(' ');
                            if (rackFields?.Length == 0)
                            {
                                logger.LogWarning("The " + SIPHeaders.SIP_HEADER_RELIABLE_ACK + " was empty.");
                            }
                            else
                            {
                                if (!Int32.TryParse(rackFields[0], out sipHeader.RAckRSeq))
                                {
                                    logger.LogWarning(SIPHeaders.SIP_HEADER_RELIABLE_ACK + " did not contain a valid integer for the RSeq being acknowledged, " + headerLine + ".");
                                }

                                if (rackFields?.Length > 1)
                                {
                                    if (!Int32.TryParse(rackFields[1], out sipHeader.RAckCSeq))
                                    {
                                        logger.LogWarning(SIPHeaders.SIP_HEADER_RELIABLE_ACK + " did not contain a valid integer for the CSeq being acknowledged, " + headerLine + ".");
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("There was no " + SIPHeaders.SIP_HEADER_RELIABLE_ACK + " method, " + headerLine + ".");
                                }

                                if (rackFields?.Length > 2)
                                {
                                    sipHeader.RAckCSeqMethod = SIPMethods.GetMethod(rackFields[2]);
                                }
                                else
                                {
                                    logger.LogWarning("There was no " + SIPHeaders.SIP_HEADER_RELIABLE_ACK + " method, " + headerLine + ".");
                                }
                            }
                        }
                        #endregion
                        #region RSeq
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_RELIABLE_SEQ.ToLower())
                        {
                            if (!Int32.TryParse(headerValue, out sipHeader.RSeq))
                            {
                                logger.LogWarning("The Rseq value was not a valid integer, " + headerLine + ".");
                            }
                        }
                        #endregion
                        #region Server
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_SERVER.ToLower())
                        {
                            sipHeader.Server = headerValue;
                        }
                        #endregion
                        #region P-Asserted-Indentity
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_PASSERTED_IDENTITY.ToLower())
                        {
                            sipHeader.PassertedIdentity.AddRange(SIPUriHeader.ParseHeader(headerValue));
                        }
                        #endregion
                        #region History-Info
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_HISTORY_INFO.ToLower())
                        {
                            sipHeader.HistoryInfo.AddRange(SIPUriHeader.ParseHeader(headerValue));
                        }
                        #endregion
                        #region Diversion
                        else if (headerNameLower == SIPHeaders.SIP_HEADER_DIVERSION.ToLower())
                        {
                            sipHeader.Diversion.AddRange(SIPUriHeader.ParseHeader(headerValue));
                        }
                        #endregion
                        else
                        {
                            sipHeader.UnknownHeaders.Add(headerLine);
                        }

                        lastHeader = headerName;
                    }
                    catch (SIPValidationException)
                    {
                        throw;
                    }
                    catch (Exception parseExcp)
                    {
                        logger.LogError("Error parsing SIP header " + headerLine + ". " + parseExcp.Message);
                        throw new SIPValidationException(SIPValidationFieldsEnum.Headers, "Unknown error parsing Header.");
                    }
                }

                // ensure History-Info Header is sorted in ascending order, if it already is nothing will be changed
                if (sipHeader.HistoryInfo != null && sipHeader.HistoryInfo.Count > 1)
                {
                    if (!SIPUriHeader.SortByUriParameter(ref sipHeader.HistoryInfo, "index"))
                    {
                        logger.LogWarning("could not sort History-Info header");
                    }
                }

                sipHeader.Validate();

                return sipHeader;
            }
            catch (SIPValidationException)
            {
                throw;
            }
            catch (Exception excp)
            {
                logger.LogError("Exception ParseSIPHeaders. " + excp.Message);
                throw new SIPValidationException(SIPValidationFieldsEnum.Headers, "Unknown error parsing Headers.");
            }
        }

        /// <summary>
        /// Puts the SIP headers together into a string ready for transmission.
        /// </summary>
        /// <returns>String representing the SIP headers.</returns>
        public new string ToString()
        {
            try
            {
                StringBuilder headersBuilder = new StringBuilder();

                headersBuilder.Append(Vias.ToString());

                string cseqField = null;
                if (this.CSeq >= 0)
                {
                    cseqField = (this.CSeqMethod != SIPMethodsEnum.NONE) ? this.CSeq + " " + this.CSeqMethod.ToString() : this.CSeq.ToString();
                }

                headersBuilder.Append((To != null) ? SIPHeaders.SIP_HEADER_TO + ": " + this.To.ToString() + m_CRLF : null);
                headersBuilder.Append((From != null) ? SIPHeaders.SIP_HEADER_FROM + ": " + this.From.ToString() + m_CRLF : null);
                headersBuilder.Append((CallId != null) ? SIPHeaders.SIP_HEADER_CALLID + ": " + this.CallId + m_CRLF : null);
                headersBuilder.Append((CSeq >= 0) ? SIPHeaders.SIP_HEADER_CSEQ + ": " + cseqField + m_CRLF : null);

                #region Appending Contact header.

                if (Contact != null && Contact.Count == 1)
                {
                    headersBuilder.Append(SIPHeaders.SIP_HEADER_CONTACT + ": " + Contact[0].ToString() + m_CRLF);
                }
                else if (Contact != null && Contact.Count > 1)
                {
                    StringBuilder contactsBuilder = new StringBuilder();
                    contactsBuilder.Append(SIPHeaders.SIP_HEADER_CONTACT + ": ");

                    bool firstContact = true;
                    foreach (SIPContactHeader contactHeader in Contact)
                    {
                        if (firstContact)
                        {
                            contactsBuilder.Append(contactHeader.ToString());
                        }
                        else
                        {
                            contactsBuilder.Append("," + contactHeader.ToString());
                        }

                        firstContact = false;
                    }

                    headersBuilder.Append(contactsBuilder.ToString() + m_CRLF);
                }

                #endregion

                headersBuilder.Append((MaxForwards >= 0) ? SIPHeaders.SIP_HEADER_MAXFORWARDS + ": " + this.MaxForwards + m_CRLF : null);
                headersBuilder.Append((Routes != null && Routes.Length > 0) ? SIPHeaders.SIP_HEADER_ROUTE + ": " + Routes.ToString() + m_CRLF : null);
                headersBuilder.Append((RecordRoutes != null && RecordRoutes.Length > 0) ? SIPHeaders.SIP_HEADER_RECORDROUTE + ": " + RecordRoutes.ToString() + m_CRLF : null);
                headersBuilder.Append((UserAgent != null && UserAgent.Trim().Length != 0) ? SIPHeaders.SIP_HEADER_USERAGENT + ": " + this.UserAgent + m_CRLF : null);
                headersBuilder.Append((Expires != -1) ? SIPHeaders.SIP_HEADER_EXPIRES + ": " + this.Expires + m_CRLF : null);
                headersBuilder.Append((MinExpires != -1) ? SIPHeaders.SIP_HEADER_MINEXPIRES + ": " + this.MinExpires + m_CRLF : null);
                headersBuilder.Append((Accept != null) ? SIPHeaders.SIP_HEADER_ACCEPT + ": " + this.Accept + m_CRLF : null);
                headersBuilder.Append((AcceptEncoding != null) ? SIPHeaders.SIP_HEADER_ACCEPTENCODING + ": " + this.AcceptEncoding + m_CRLF : null);
                headersBuilder.Append((AcceptLanguage != null) ? SIPHeaders.SIP_HEADER_ACCEPTLANGUAGE + ": " + this.AcceptLanguage + m_CRLF : null);
                headersBuilder.Append((Allow != null) ? SIPHeaders.SIP_HEADER_ALLOW + ": " + this.Allow + m_CRLF : null);
                headersBuilder.Append((AlertInfo != null) ? SIPHeaders.SIP_HEADER_ALERTINFO + ": " + this.AlertInfo + m_CRLF : null);
                headersBuilder.Append((AuthenticationInfo != null) ? SIPHeaders.SIP_HEADER_AUTHENTICATIONINFO + ": " + this.AuthenticationInfo + m_CRLF : null);

                if (AuthenticationHeaders.Count > 0)
                {
                    foreach (var authHeader in AuthenticationHeaders)
                    {
                        var value = authHeader.ToString();
                        if (value != null)
                        {
                            headersBuilder.Append(authHeader.ToString() + m_CRLF);
                        }
                    }
                }
                headersBuilder.Append((CallInfo != null) ? SIPHeaders.SIP_HEADER_CALLINFO + ": " + this.CallInfo + m_CRLF : null);
                headersBuilder.Append((ContentDisposition != null) ? SIPHeaders.SIP_HEADER_CONTENT_DISPOSITION + ": " + this.ContentDisposition + m_CRLF : null);
                headersBuilder.Append((ContentEncoding != null) ? SIPHeaders.SIP_HEADER_CONTENT_ENCODING + ": " + this.ContentEncoding + m_CRLF : null);
                headersBuilder.Append((ContentLanguage != null) ? SIPHeaders.SIP_HEADER_CONTENT_LANGUAGE + ": " + this.ContentLanguage + m_CRLF : null);
                headersBuilder.Append((Date != null) ? SIPHeaders.SIP_HEADER_DATE + ": " + Date + m_CRLF : null);
                headersBuilder.Append((ErrorInfo != null) ? SIPHeaders.SIP_HEADER_ERROR_INFO + ": " + this.ErrorInfo + m_CRLF : null);
                headersBuilder.Append((InReplyTo != null) ? SIPHeaders.SIP_HEADER_IN_REPLY_TO + ": " + this.InReplyTo + m_CRLF : null);
                headersBuilder.Append((Organization != null) ? SIPHeaders.SIP_HEADER_ORGANIZATION + ": " + this.Organization + m_CRLF : null);
                headersBuilder.Append((Priority != null) ? SIPHeaders.SIP_HEADER_PRIORITY + ": " + Priority + m_CRLF : null);
                headersBuilder.Append((ProxyRequire != null) ? SIPHeaders.SIP_HEADER_PROXY_REQUIRE + ": " + this.ProxyRequire + m_CRLF : null);
                headersBuilder.Append((ReplyTo != null) ? SIPHeaders.SIP_HEADER_REPLY_TO + ": " + this.ReplyTo + m_CRLF : null);
                headersBuilder.Append((Require != null) ? SIPHeaders.SIP_HEADER_REQUIRE + ": " + Require + m_CRLF : null);
                headersBuilder.Append((RetryAfter != null) ? SIPHeaders.SIP_HEADER_RETRY_AFTER + ": " + this.RetryAfter + m_CRLF : null);
                headersBuilder.Append((Server != null && Server.Trim().Length != 0) ? SIPHeaders.SIP_HEADER_SERVER + ": " + this.Server + m_CRLF : null);
                headersBuilder.Append((Subject != null) ? SIPHeaders.SIP_HEADER_SUBJECT + ": " + Subject + m_CRLF : null);
                headersBuilder.Append((Supported != null) ? SIPHeaders.SIP_HEADER_SUPPORTED + ": " + Supported + m_CRLF : null);
                headersBuilder.Append((Timestamp != null) ? SIPHeaders.SIP_HEADER_TIMESTAMP + ": " + Timestamp + m_CRLF : null);
                headersBuilder.Append((Unsupported != null) ? SIPHeaders.SIP_HEADER_UNSUPPORTED + ": " + Unsupported + m_CRLF : null);
                headersBuilder.Append((Warning != null) ? SIPHeaders.SIP_HEADER_WARNING + ": " + Warning + m_CRLF : null);
                headersBuilder.Append((ETag != null) ? SIPHeaders.SIP_HEADER_ETAG + ": " + ETag + m_CRLF : null);
                headersBuilder.Append(SIPHeaders.SIP_HEADER_CONTENTLENGTH + ": " + this.ContentLength + m_CRLF);
                if (this.ContentType != null && this.ContentType.Trim().Length > 0)
                {
                    headersBuilder.Append(SIPHeaders.SIP_HEADER_CONTENTTYPE + ": " + this.ContentType + m_CRLF);
                }

                // Non-core SIP headers.
                headersBuilder.Append((AllowEvents != null) ? SIPHeaders.SIP_HEADER_ALLOW_EVENTS + ": " + AllowEvents + m_CRLF : null);
                headersBuilder.Append((Event != null) ? SIPHeaders.SIP_HEADER_EVENT + ": " + Event + m_CRLF : null);
                headersBuilder.Append((SubscriptionState != null) ? SIPHeaders.SIP_HEADER_SUBSCRIPTION_STATE + ": " + SubscriptionState + m_CRLF : null);
                headersBuilder.Append((ReferSub != null) ? SIPHeaders.SIP_HEADER_REFERSUB + ": " + ReferSub + m_CRLF : null);
                headersBuilder.Append((ReferTo != null) ? SIPHeaders.SIP_HEADER_REFERTO + ": " + ReferTo + m_CRLF : null);
                headersBuilder.Append((ReferredBy != null) ? SIPHeaders.SIP_HEADER_REFERREDBY + ": " + ReferredBy + m_CRLF : null);
                headersBuilder.Append((Replaces != null) ? SIPHeaders.SIP_HEADER_REPLACES + ": " + Replaces + m_CRLF : null);
                headersBuilder.Append((Reason != null) ? SIPHeaders.SIP_HEADER_REASON + ": " + Reason + m_CRLF : null);
                headersBuilder.Append((RSeq != -1) ? SIPHeaders.SIP_HEADER_RELIABLE_SEQ + ": " + RSeq + m_CRLF : null);
                headersBuilder.Append((RAckRSeq != -1) ? SIPHeaders.SIP_HEADER_RELIABLE_ACK + ": " + RAckRSeq + " " + RAckCSeq + " " + RAckCSeqMethod + m_CRLF : null);

                foreach (var PAI in PassertedIdentity)
                {
                    headersBuilder.Append(SIPHeaders.SIP_HEADER_PASSERTED_IDENTITY + ": " + PAI + m_CRLF);
                }

                foreach (var HistInfo in HistoryInfo)
                {
                    headersBuilder.Append(SIPHeaders.SIP_HEADER_HISTORY_INFO + ": " + HistInfo + m_CRLF);
                }

                foreach (var DiversionHeader in Diversion)
                {
                    headersBuilder.Append(SIPHeaders.SIP_HEADER_DIVERSION + ": " + DiversionHeader + m_CRLF);
                }

                // Custom SIP headers.
                headersBuilder.Append((ProxyReceivedFrom != null) ? SIPHeaders.SIP_HEADER_PROXY_RECEIVEDFROM + ": " + ProxyReceivedFrom + m_CRLF : null);
                headersBuilder.Append((ProxyReceivedOn != null) ? SIPHeaders.SIP_HEADER_PROXY_RECEIVEDON + ": " + ProxyReceivedOn + m_CRLF : null);
                headersBuilder.Append((ProxySendFrom != null) ? SIPHeaders.SIP_HEADER_PROXY_SENDFROM + ": " + ProxySendFrom + m_CRLF : null);

                // Unknown SIP headers
                foreach (string unknownHeader in UnknownHeaders)
                {
                    headersBuilder.Append(unknownHeader + m_CRLF);
                }

                return headersBuilder.ToString();
            }
            catch (Exception excp)
            {
                logger.LogError("Exception SIPHeader ToString. " + excp.Message);
                throw;
            }
        }

        private void Validate()
        {
            if (Vias == null || Vias.Length == 0)
            {
                throw new SIPValidationException(SIPValidationFieldsEnum.ViaHeader, "Invalid header, no Via.");
            }
        }

        public void SetDateHeader()
        {
            Date = DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss ") + "GMT";
        }

        public void SetDateHeader(bool useLocalTime, string timeFormat)
        {
            var time = useLocalTime ? DateTime.Now : DateTime.UtcNow;
            Date = time.ToString(timeFormat) + (useLocalTime ? "" : "GMT");
        }

        public SIPHeader Copy()
        {
            string headerString = this.ToString();
            string[] sipHeaders = SIPHeader.SplitHeaders(headerString);
            return ParseSIPHeaders(sipHeaders);
        }

        public string GetUnknownHeaderValue(string unknownHeaderName)
        {
            if (unknownHeaderName.IsNullOrBlank())
            {
                return null;
            }
            else if (UnknownHeaders == null || UnknownHeaders.Count == 0)
            {
                return null;
            }
            else
            {
                foreach (string unknonwHeader in UnknownHeaders)
                {
                    string trimmedHeader = unknonwHeader.Trim();
                    int delimiterIndex = trimmedHeader.IndexOf(SIPConstants.HEADER_DELIMITER_CHAR);

                    if (delimiterIndex == -1)
                    {
                        logger.LogWarning("Invalid SIP header, ignoring, " + unknonwHeader + ".");
                        continue;
                    }

                    string headerName = trimmedHeader.Substring(0, delimiterIndex).Trim();

                    if (headerName.ToLower() == unknownHeaderName.ToLower())
                    {
                        return trimmedHeader.Substring(delimiterIndex + 1).Trim();
                    }
                }

                return null;
            }
        }
    }
}
