//-----------------------------------------------------------------------------
// Filename: IceServer.cs
//
// Description: Encapsulates the connection details for a TURN/STUN server.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 22 Jun 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using SIPSorcery.Sys;

namespace SIPSorcery.Net
{
    /// <summary>
    /// If ICE servers (STUN or TURN) are being used with the session this class is used to track
    /// the connection state for each server that gets used.
    /// </summary>
    public class IceServer
    {
        /// <summary>
        /// The maximum number of requests to send to an ICE server without getting 
        /// a response.
        /// </summary>
        internal const int MAX_REQUESTS = 25;
        
        /// <summary>
        /// The STUN error code response indicating an authenticated request is required.
        /// </summary>
        internal const int STUN_UNAUTHORISED_ERROR_CODE = 401;

        /// <summary>
        /// The STUN error code response indicating a stale nonce
        /// </summary>
        internal const int STUN_STALE_NONCE_ERROR_CODE = 438;

        internal STUNUri _uri;
        internal string _username;
        internal string _password;
        
        /// <summary>
        /// The end point for this STUN or TURN server. Will be set asynchronously once
        /// any required DNS lookup completes.
        /// </summary>
        internal IPEndPoint ServerEndPoint { get; set; }

        /// <summary>
        /// The number of requests that have been sent to the server without
        /// a response.
        /// </summary>
        internal int OutstandingRequestsSent { get; set; }
        
        /// <summary>
        /// This field records the time when allocation expires
        /// </summary>
        public DateTime TurnTimeToExpiry { get; set; } = DateTime.MinValue;
        
        /// <summary>
        /// If requests to the server need to be authenticated this is the nonce to set. 
        /// Normally the nonce will come from the server in a 401 Unauthorized response.
        /// </summary>
        internal byte[] Nonce { get; set; }

        /// <summary>
        /// If requests to the server need to be authenticated this is the realm to set. 
        /// The realm may be known in advance or can come from the server in a 401 
        /// Unauthorized response.
        /// </summary>
        internal byte[] Realm { get; set; }
        
        public ProtocolType Protocol { get { return _uri.Protocol; } }
        
        /// <summary>
        /// Extracts the fields required for authentication from a STUN error response.
        /// </summary>
        /// <param name="stunResponse">The STUN authentication required error response.</param>
        internal void SetAuthenticationFields(STUNMessage stunResponse)
        {
            // Set the authentication properties authenticate.
            var nonceAttribute = stunResponse.Attributes.FirstOrDefault(x => x.AttributeType == STUNAttributeTypesEnum.Nonce);
            Nonce = nonceAttribute?.Value;

            var realmAttribute = stunResponse.Attributes.FirstOrDefault(x => x.AttributeType == STUNAttributeTypesEnum.Realm);
            Realm = realmAttribute?.Value;
        }
    }
}
