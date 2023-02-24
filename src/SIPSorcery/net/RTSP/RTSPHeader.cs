//-----------------------------------------------------------------------------
// Filename: RTSPHeader.cs
//
// Description: RTSP header.
//
// Author(s):
// Aaron Clauson
//
// History:
// 09 Nov 2007	Aaron Clauson	Created (aaron@sipsorcery.com), SIP Sorcery PTY LTD, Hobart, Australia (www.sipsorcery.com).
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

namespace SIPSorcery.Net
{
    public class RTSPHeader
    {
        private static string m_CRLF = RTSPConstants.CRLF;

        private static ILogger logger = Log.Logger;

        private static char[] delimiterChars = new char[] { ':' };

        public string Accept;
        public string ContentType;
        public int ContentLength;
        public int CSeq = -1;
        public string Session;
        public RTSPTransportHeader Transport;

        public List<string> UnknownHeaders = new List<string>();    // Holds any unrecognised headers.

        public string RawCSeq;

        public RTSPHeaderParserError CSeqParserError = RTSPHeaderParserError.None;

        private RTSPHeader()
        { }

        public RTSPHeader(int cseq, string session)
        {
            CSeq = cseq;
            Session = session;
        }

        public static string[] SplitHeaders(string message)
        {
            // SIP headers can be extended across lines if the first character of the next line is at least on whitespace character.
            message = Regex.Replace(message, m_CRLF + @"\s+", " ", RegexOptions.Singleline);

            // Some user agents couldn't get the \r\n bit right.
            message = Regex.Replace(message, "\r ", m_CRLF, RegexOptions.Singleline);

            return Regex.Split(message, m_CRLF);
        }

        public static RTSPHeader ParseRTSPHeaders(string[] headersCollection)
        {
            try
            {
                RTSPHeader rtspHeader = new RTSPHeader();

                string lastHeader = null;

                for (int lineIndex = 0; lineIndex < headersCollection.Length; lineIndex++)
                {
                    string headerLine = headersCollection[lineIndex];

                    if (headerLine == null || headerLine.Trim().Length == 0)
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
                        string[] headerParts = headerLine.Trim().Split(delimiterChars, 2);

                        if (headerParts == null || headerParts.Length < 2)
                        {
                            logger.LogError("Invalid RTSP header, ignoring. header=" + headerLine + ".");

                            try
                            {
                                string errorHeaders = String.Join(m_CRLF, headersCollection);
                                logger.LogError("Full Invalid Headers: " + errorHeaders);
                            }
                            catch { }

                            continue;
                        }

                        headerName = headerParts[0].Trim();
                        headerValue = headerParts[1].Trim();
                    }

                    try
                    {
                        string headerNameLower = headerName.ToLower();

                        #region Accept
                        if (headerNameLower == RTSPHeaders.RTSP_HEADER_ACCEPT.ToLower())
                        {
                            rtspHeader.Accept = headerValue;
                        }
                        #endregion
                        #region ContentType
                        if (headerNameLower == RTSPHeaders.RTSP_HEADER_CONTENTTYPE.ToLower())
                        {
                            rtspHeader.ContentType = headerValue;
                        }
                        #endregion
                        #region ContentLength
                        if (headerNameLower == RTSPHeaders.RTSP_HEADER_CONTENTLENGTH.ToLower())
                        {
                            rtspHeader.RawCSeq = headerValue;

                            if (headerValue == null || headerValue.Trim().Length == 0)
                            {
                                logger.LogWarning("Invalid RTSP header, the " + RTSPHeaders.RTSP_HEADER_CONTENTLENGTH + " was empty.");
                            }
                            else if (!Int32.TryParse(headerValue.Trim(), out rtspHeader.ContentLength))
                            {
                                logger.LogWarning("Invalid RTSP header, the " + RTSPHeaders.RTSP_HEADER_CONTENTLENGTH + " was not a valid 32 bit integer, " + headerValue + ".");
                            }
                        }
                        #endregion
                        #region CSeq
                        if (headerNameLower == RTSPHeaders.RTSP_HEADER_CSEQ.ToLower())
                        {
                            rtspHeader.RawCSeq = headerValue;

                            if (headerValue == null || headerValue.Trim().Length == 0)
                            {
                                rtspHeader.CSeqParserError = RTSPHeaderParserError.CSeqEmpty;
                                logger.LogWarning("Invalid RTSP header, the " + RTSPHeaders.RTSP_HEADER_CSEQ + " was empty.");
                            }
                            else if (!Int32.TryParse(headerValue.Trim(), out rtspHeader.CSeq))
                            {
                                rtspHeader.CSeqParserError = RTSPHeaderParserError.CSeqNotValidInteger;
                                logger.LogWarning("Invalid SIP header, the " + RTSPHeaders.RTSP_HEADER_CSEQ + " was not a valid 32 bit integer, " + headerValue + ".");
                            }
                        }
                        #endregion
                        #region Session
                        if (headerNameLower == RTSPHeaders.RTSP_HEADER_SESSION.ToLower())
                        {
                            rtspHeader.Session = headerValue;
                        }
                        #endregion
                        #region Transport
                        if (headerNameLower == RTSPHeaders.RTSP_HEADER_TRANSPORT.ToLower())
                        {
                            rtspHeader.Transport = RTSPTransportHeader.Parse(headerValue);
                        }
                        #endregion
                        else
                        {
                            rtspHeader.UnknownHeaders.Add(headerLine);
                        }

                        lastHeader = headerName;
                    }
                    catch (Exception parseExcp)
                    {
                        logger.LogError("Error parsing RTSP header " + headerLine + ". " + parseExcp.Message);
                        throw;
                    }
                }

                //sipHeader.Valid = sipHeader.Validate(out sipHeader.ValidationError);

                return rtspHeader;
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception excp)
            {
                logger.LogError("Exception ParseRTSPHeaders. " + excp.Message);
                throw;
            }
        }


        public new string ToString()
        {
            try
            {
                StringBuilder headersBuilder = new StringBuilder();

                headersBuilder.Append((CSeq > 0) ? RTSPHeaders.RTSP_HEADER_CSEQ + ": " + CSeq + m_CRLF : null);
                headersBuilder.Append((Session != null) ? RTSPHeaders.RTSP_HEADER_SESSION + ": " + Session + m_CRLF : null);
                headersBuilder.Append((Accept != null) ? RTSPHeaders.RTSP_HEADER_ACCEPT + ": " + Accept + m_CRLF : null);
                headersBuilder.Append((ContentType != null) ? RTSPHeaders.RTSP_HEADER_CONTENTTYPE + ": " + ContentType + m_CRLF : null);
                headersBuilder.Append((ContentLength != 0) ? RTSPHeaders.RTSP_HEADER_CONTENTLENGTH + ": " + ContentLength + m_CRLF : null);
                headersBuilder.Append((Transport != null) ? RTSPHeaders.RTSP_HEADER_TRANSPORT + ": " + Transport.ToString() + m_CRLF : null);

                return headersBuilder.ToString();
            }
            catch (Exception excp)
            {
                logger.LogError("Exception RTSPHeader ToString. " + excp.Message);
                throw;
            }
        }
    }
}
