namespace SIPSorcery.SIP
{
    public static class SIPHeaders
    {
        // SIP Header Keys.
        public const string SIP_HEADER_ACCEPT = "Accept";
        public const string SIP_HEADER_ACCEPTENCODING = "Accept-Encoding";
        public const string SIP_HEADER_ACCEPTLANGUAGE = "Accept-Language";
        public const string SIP_HEADER_ALERTINFO = "Alert-Info";
        public const string SIP_HEADER_ALLOW = "Allow";
        public const string SIP_HEADER_ALLOW_EVENTS = "Allow-Events";               // RC3265 (SIP Events).
        public const string SIP_HEADER_AUTHENTICATIONINFO = "Authentication-Info";
        public const string SIP_HEADER_AUTHORIZATION = "Authorization";
        public const string SIP_HEADER_CALLID = "Call-ID";
        public const string SIP_HEADER_CALLINFO = "Call-Info";
        public const string SIP_HEADER_CONTACT = "Contact";
        public const string SIP_HEADER_CONTENT_DISPOSITION = "Content-Disposition";
        public const string SIP_HEADER_CONTENT_ENCODING = "Content-Encoding";
        public const string SIP_HEADER_CONTENT_LANGUAGE = "Content-Language";
        public const string SIP_HEADER_CONTENTLENGTH = "Content-Length";
        public const string SIP_HEADER_CONTENTTYPE = "Content-Type";
        public const string SIP_HEADER_CSEQ = "CSeq";
        public const string SIP_HEADER_DATE = "Date";
        public const string SIP_HEADER_ERROR_INFO = "Error-Info";
        public const string SIP_HEADER_EVENT = "Event";                             // RC3265 (SIP Events).
        public const string SIP_HEADER_ETAG = "SIP-ETag";                           // RFC3903
        public const string SIP_HEADER_EXPIRES = "Expires";
        public const string SIP_HEADER_FROM = "From";
        public const string SIP_HEADER_IN_REPLY_TO = "In-Reply-To";
        public const string SIP_HEADER_MAXFORWARDS = "Max-Forwards";
        public const string SIP_HEADER_MINEXPIRES = "Min-Expires";
        public const string SIP_HEADER_MIME_VERSION = "MIME-Version";
        public const string SIP_HEADER_ORGANIZATION = "Organization";
        public const string SIP_HEADER_PRIORITY = "Priority";
        public const string SIP_HEADER_PROXYAUTHENTICATION = "Proxy-Authenticate";
        public const string SIP_HEADER_PROXYAUTHORIZATION = "Proxy-Authorization";
        public const string SIP_HEADER_PROXY_REQUIRE = "Proxy-Require";
        public const string SIP_HEADER_RELIABLE_ACK = "RAck";                       // RFC 3262 "The RAck header is sent in a PRACK request to support reliability of provisional responses."
        public const string SIP_HEADER_REASON = "Reason";
        public const string SIP_HEADER_RECORDROUTE = "Record-Route";
        public const string SIP_HEADER_REFERREDBY = "Referred-By";                  // RFC 3515 "The Session Initiation Protocol (SIP) Refer Method".
        public const string SIP_HEADER_REFERSUB = "Refer-Sub";                      // RFC 4488 Used to stop the implicit SIP event subscription on a REFER request.
        public const string SIP_HEADER_REFERTO = "Refer-To";                        // RFC 3515 "The Session Initiation Protocol (SIP) Refer Method".
        public const string SIP_HEADER_REPLY_TO = "Reply-To";
        public const string SIP_HEADER_REPLACES = "Replaces";
        public const string SIP_HEADER_REQUIRE = "Require";
        public const string SIP_HEADER_RETRY_AFTER = "Retry-After";
        public const string SIP_HEADER_RELIABLE_SEQ = "RSeq";                     // RFC 3262 "The RSeq header is used in provisional responses in order to transmit them reliably."
        public const string SIP_HEADER_ROUTE = "Route";
        public const string SIP_HEADER_SERVER = "Server";
        public const string SIP_HEADER_SUBJECT = "Subject";
        public const string SIP_HEADER_SUBSCRIPTION_STATE = "Subscription-State";       // RC3265 (SIP Events).
        public const string SIP_HEADER_SUPPORTED = "Supported";
        public const string SIP_HEADER_TIMESTAMP = "Timestamp";
        public const string SIP_HEADER_TO = "To";
        public const string SIP_HEADER_UNSUPPORTED = "Unsupported";
        public const string SIP_HEADER_USERAGENT = "User-Agent";
        public const string SIP_HEADER_VIA = "Via";
        public const string SIP_HEADER_WARNING = "Warning";
        public const string SIP_HEADER_WWWAUTHENTICATE = "WWW-Authenticate";
        public const string SIP_HEADER_PASSERTED_IDENTITY = "P-Asserted-Identity";          // RFC 3325
        public const string SIP_HEADER_HISTORY_INFO = "History-Info";                       // RFC 4244
        public const string SIP_HEADER_DIVERSION = "Diversion";                             // RFC 5806

        // SIP Compact Header Keys.
        public const string SIP_COMPACTHEADER_ALLOWEVENTS = "u";        // RC3265 (SIP Events).
        public const string SIP_COMPACTHEADER_CALLID = "i";
        public const string SIP_COMPACTHEADER_CONTACT = "m";
        public const string SIP_COMPACTHEADER_CONTENTLENGTH = "l";
        public const string SIP_COMPACTHEADER_CONTENTTYPE = "c";
        public const string SIP_COMPACTHEADER_EVENT = "o";              // RC3265 (SIP Events).
        public const string SIP_COMPACTHEADER_FROM = "f";
        public const string SIP_COMPACTHEADER_REFERTO = "r";            // RFC 3515 "The Session Initiation Protocol (SIP) Refer Method".
        public const string SIP_COMPACTHEADER_SUBJECT = "s";
        public const string SIP_COMPACTHEADER_SUPPORTED = "k";
        public const string SIP_COMPACTHEADER_TO = "t";
        public const string SIP_COMPACTHEADER_VIA = "v";

        // Custom SIP headers to allow proxy to communicate network info to internal servers.
        public const string SIP_HEADER_PROXY_RECEIVEDON = "Proxy-ReceivedOn";
        public const string SIP_HEADER_PROXY_RECEIVEDFROM = "Proxy-ReceivedFrom";
        public const string SIP_HEADER_PROXY_SENDFROM = "Proxy-SendFrom";
    }
}