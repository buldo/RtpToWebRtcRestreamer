using System;

namespace SIPSorcery.Net
{
    public static class STUNConstants
    {
        public const int DEFAULT_STUN_PORT = 3478;
        public const int DEFAULT_STUN_TLS_PORT = 5349;
        public const int DEFAULT_TURN_PORT = 3478;
        public const int DEFAULT_TURN_TLS_PORT = 5349;

        public static int GetPortForScheme(STUNSchemesEnum scheme)
        {
            switch (scheme)
            {
                case STUNSchemesEnum.stun:
                    return DEFAULT_STUN_PORT;
                case STUNSchemesEnum.stuns:
                    return DEFAULT_STUN_TLS_PORT;
                case STUNSchemesEnum.turn:
                    return DEFAULT_TURN_PORT;
                case STUNSchemesEnum.turns:
                    return DEFAULT_TURN_TLS_PORT;
                default:
                    throw new ApplicationException("STUN or TURN scheme not recognised in STUNConstants.GetPortForScheme.");
            }
        }
    }
}