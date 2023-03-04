using System.Text.Json;
using Bld.RtpToWebRtcRestreamer.RtpNg;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC
{
    /// <summary>
    /// Initialiser for the RTCSessionDescription instance.
    /// </summary>
    /// <remarks>
    /// As specified in https://www.w3.org/TR/webrtc/#rtcsessiondescription-class.
    /// </remarks>
    internal class RTCSessionDescriptionInit
    {
        /// <summary>
        /// The type of the Session Description.
        /// </summary>
        public RTCSdpType type { get; set; }

        /// <summary>
        /// A string representation of the Session Description.
        /// </summary>
        public string sdp { get; set; }

        public string toJSON()
        {
            //return "{" +
            //    $"  \"type\": \"{type}\"," +
            //    $"  \"sdp\": \"{sdp.Replace(SDP.CRLF, @"\\n").Replace("\"", "\\\"")}\"" +
            //    "}";

            return JsonSerializer.Serialize(this, Constants.JsonSerializerOptions);
        }

        public static bool TryParse(string json, out RTCSessionDescriptionInit init)
        {
            init = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            init = JsonSerializer.Deserialize<RTCSessionDescriptionInit>(json, Constants.JsonSerializerOptions);

            // To qualify as parsed all required fields must be set.
            return init != null &&
                   init.sdp != null;
        }
    }
}