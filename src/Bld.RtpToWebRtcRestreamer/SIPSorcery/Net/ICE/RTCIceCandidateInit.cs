using System.Text.Json;
using Bld.RtpToWebRtcRestreamer.RtpNg;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// Represents an ICE candidate and associated properties that link it to the SDP.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#dom-rtcicecandidateinit.
/// </remarks>
internal class RTCIceCandidateInit
{
    public string candidate { get; set; }
    public string sdpMid { get; set; }
    public ushort sdpMLineIndex { get; set; }
    public string usernameFragment { get; set; }

    public string toJSON()
    {
        //return "{" +
        //     $"  \"sdpMid\": \"{sdpMid ?? sdpMLineIndex.ToString()}\"," +
        //     $"  \"sdpMLineIndex\": {sdpMLineIndex}," +
        //     $"  \"usernameFragment\": \"{usernameFragment}\"," +
        //     $"  \"candidate\": \"{candidate}\"" +
        //     "}";

        return JsonSerializer.Serialize(this, Constants.JsonSerializerOptions);
    }

    public static bool TryParse(string json, out RTCIceCandidateInit init)
    {
        //init = JsonSerializer.Deserialize< RTCIceCandidateInit>(json);

        init = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        init = JsonSerializer.Deserialize<RTCIceCandidateInit>(json, Constants.JsonSerializerOptions);

        // To qualify as parsed all required fields must be set.
        return init != null &&
               init.candidate != null &&
               init.sdpMid != null;
    }
}