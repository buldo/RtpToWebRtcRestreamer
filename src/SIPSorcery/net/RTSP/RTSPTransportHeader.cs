using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// Represents the Transport header used in RTSP requests and responses.
    /// </summary>
    public class RTSPTransportHeader
    {
        public const string DESTINATION_FIELD_NAME = "destination";
        public const string SOURCE_FIELD_NAME = "source";
        public const string MULTICAST_RTP_PORT_FIELD_NAME = "port";
        public const string CLIENT_RTP_PORT_FIELD_NAME = "client_port";
        public const string SERVER_RTP_PORT_FIELD_NAME = "server_port";
        public const string MODE_FIELD_NAME = "mode";

        private const string DEFAULT_TRANSPORT_SPECIFIER = "RTP/AVP/UDP";
        private const string DEFAULT_BROADCAST_TYPE = "unicast";

        private static ILogger logger = Log.Logger;

        public string RawHeader;

        public string TransportSpecifier;   // RTP/AVP{/[TCP/UDP]}, default is UDP.
        public string BroadcastType;        // Unicast or multicast.
        public string Destination;
        public string Source;
        public string MulticastRTPPortRange;// e.g. port=3456-3457.
        public string ClientRTPPortRange;   // e.g. client_port=3456-3457.
        public string ServerRTPPortRange;   // e.g. server_port=3456-3457.
        public string Mode;

        public RTSPTransportHeader()
        {
            TransportSpecifier = DEFAULT_TRANSPORT_SPECIFIER;
            BroadcastType = DEFAULT_BROADCAST_TYPE;
        }

        public static RTSPTransportHeader Parse(string header)
        {
            var transportHeader = new RTSPTransportHeader();

            if (header.NotNullOrBlank())
            {
                transportHeader.RawHeader = header;

                string[] fields = header.Split(';');

                transportHeader.TransportSpecifier = fields[0];
                transportHeader.BroadcastType = fields[1];

                foreach (string field in fields.Where(x => x.Contains('=')))
                {
                    string fieldName = field.Split('=')[0];
                    string fieldValue = field.Split('=')[1];

                    switch (fieldName.ToLower())
                    {
                        case CLIENT_RTP_PORT_FIELD_NAME:
                            transportHeader.ClientRTPPortRange = fieldValue.Trim();
                            break;
                        case DESTINATION_FIELD_NAME:
                            transportHeader.Destination = fieldValue.Trim();
                            break;
                        case SERVER_RTP_PORT_FIELD_NAME:
                            transportHeader.ServerRTPPortRange = fieldValue.Trim();
                            break;
                        case SOURCE_FIELD_NAME:
                            transportHeader.Source = fieldValue.Trim();
                            break;
                        case MODE_FIELD_NAME:
                            transportHeader.Mode = fieldValue.Trim();
                            break;
                        default:
                            logger.LogWarning("An RTSP Transport header parameter was not recognised. " + field);
                            break;
                    }
                }
            }

            return transportHeader;
        }

        /// <summary>
        /// Attempts to determine the client RTP port based on the transport header attributes.
        /// </summary>
        /// <returns>The client port that RTP packets should be sent to. If the port cannot be determined then 0.</returns>
        public int GetClientRTPPort()
        {
            if (ClientRTPPortRange.NotNullOrBlank())
            {
                int clientRTPPort = 0;

                var fields = ClientRTPPortRange.Split('-');

                if (Int32.TryParse(fields[0], out clientRTPPort))
                {
                    return clientRTPPort;
                }
            }

            return 0;
        }

        /// <summary>
        /// Attempts to determine the client RTCP port based on the transport header attributes.
        /// </summary>
        /// <returns>The client port that RTCP packets should be sent to. If the port cannot be determined then 0.</returns>
        public int GetClientRtcpPort()
        {
            if (ClientRTPPortRange.NotNullOrBlank())
            {
                int clientRTCPPort = 0;

                var fields = ClientRTPPortRange.Split('-');

                if (fields.Length > 1 && Int32.TryParse(fields[1], out clientRTCPPort))
                {
                    return clientRTCPPort;
                }
            }

            return 0;
        }


        /// <summary>
        /// Attempts to determine the server RTP port based on the transport header attributes.
        /// </summary>
        /// <returns>The server port that RTP packets should be sent to. If the port cannot be determined then 0.</returns>
        public int GetServerRTPPort()
        {
            if (ServerRTPPortRange.NotNullOrBlank())
            {
                int serverRTPPort = 0;

                var fields = ServerRTPPortRange.Split('-');

                if (Int32.TryParse(fields[0], out serverRTPPort))
                {
                    return serverRTPPort;
                }
            }

            return 0;
        }

        /// <summary>
        /// Attempts to determine the server Rtcp port based on the transport header attributes.
        /// </summary>
        /// <returns>The server port that RTCP packets should be sent to. If the port cannot be determined then 0.</returns>
        public int GetServerRtcpPort()
        {
            if (ServerRTPPortRange.NotNullOrBlank())
            {
                int serverRtcpPort = 0;

                var fields = ServerRTPPortRange.Split('-');

                if (fields.Length > 0 && Int32.TryParse(fields[1], out serverRtcpPort))
                {
                    return serverRtcpPort;
                }
            }

            return 0;
        }

        public override string ToString()
        {
            string transportHeader = TransportSpecifier + ";" + BroadcastType;

            if (Destination.NotNullOrBlank())
            {
                transportHeader += String.Format(";{0}={1}", DESTINATION_FIELD_NAME, Destination);
            }

            if (Source.NotNullOrBlank())
            {
                transportHeader += String.Format(";{0}={1}", SOURCE_FIELD_NAME, Source);
            }

            if (ClientRTPPortRange.NotNullOrBlank())
            {
                transportHeader += String.Format(";{0}={1}", CLIENT_RTP_PORT_FIELD_NAME, ClientRTPPortRange);
            }

            if (ServerRTPPortRange.NotNullOrBlank())
            {
                transportHeader += String.Format(";{0}={1}", SERVER_RTP_PORT_FIELD_NAME, ServerRTPPortRange);
            }

            if (Mode.NotNullOrBlank())
            {
                transportHeader += String.Format(";{0}={1}", MODE_FIELD_NAME, Mode);
            }

            return transportHeader;
        }
    }
}