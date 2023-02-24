namespace SIPSorcery.SIP
{
    /// <summary>
    /// Authorization Headers
    /// </summary>
    public static class AuthHeaders
    {
        public const string AUTH_DIGEST_KEY = "Digest";
        public const string AUTH_REALM_KEY = "realm";
        public const string AUTH_NONCE_KEY = "nonce";
        public const string AUTH_USERNAME_KEY = "username";
        public const string AUTH_RESPONSE_KEY = "response";
        public const string AUTH_URI_KEY = "uri";
        public const string AUTH_ALGORITHM_KEY = "algorithm";
        public const string AUTH_CNONCE_KEY = "cnonce";
        public const string AUTH_NONCECOUNT_KEY = "nc";
        public const string AUTH_QOP_KEY = "qop";
        public const string AUTH_OPAQUE_KEY = "opaque";
    }
}