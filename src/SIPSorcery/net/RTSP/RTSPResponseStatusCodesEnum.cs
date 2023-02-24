namespace SIPSorcery.Net
{
    public enum RTSPResponseStatusCodesEnum
    {
        // 1xx Informational.
        Continue = 100,

        // 2xx Success.
        OK = 200,
        Created = 201,
        LowOnStorageSpace = 250,

        // 3xx Redirection.
        MultipleChoices = 300,
        MovedPermanently = 301,
        MovedTemporarily = 302,
        SeeOther = 303,
        UseProxy = 305,

        // 4xx Client Error.
        BadRequest = 400,
        Unauthorized = 401,
        PaymentRequired = 402,
        Forbidden = 403,
        NotFound = 404,
        MethodNotAllowed = 405,
        NotAcceptable = 406,
        ProxyAuthenticationRequired = 407,

        // 5xx Server Error.
        InternalServerError = 500,
        NotImplemented = 501,
        BadGateway = 502,
        ServiceUnavailable = 503,
        GatewayTimeOut = 504,
        RTSPVersionNotSupported = 505,
        OptionNotSupported = 551,
    }
}