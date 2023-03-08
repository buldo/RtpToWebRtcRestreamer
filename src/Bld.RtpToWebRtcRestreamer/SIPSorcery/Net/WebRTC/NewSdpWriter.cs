using System.Text;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;
internal static class NewSdpWriter
{
    public static string GetSdpString(SDP.SDP sdp)
    {
        var sb = new StringBuilder();
        sb.AppendLine("v=0");
        sb.AppendLine($"o={sdp.Owner}");
        sb.AppendLine($"s={sdp.SessionName}");
        sb.AppendLine($"t={sdp.Timing}");

        sb.AppendLine("a=group:BUNDLE v");
        sb.AppendLine("a=ice-options:ice2");
        sb.AppendLine($"a=fingerprint:{sdp.Media.First().DtlsFingerprint}");
        sb.AppendLine("a=extmap-allow-mixed");
        sb.AppendLine("a=msid-semantic: WMS *");

        foreach (var media in sdp.Media
                     .OrderBy(x => x.MLineIndex)
                     .ThenBy(x => x.MediaID))
        {
            sb.AppendLine($"m={media.Media} {media.Port} {media.Transport} {media.GetFormatListToString()}");
            sb.AppendLine($"c={media.Connection.ConnectionNetworkType} {media.Connection.ConnectionAddressType} {media.Connection.ConnectionAddress}");
            sb.AppendLine("a=sendonly");
            sb.AppendLine("a=mid:v");
            sb.AppendLine("a=rtcp-mux");
            sb.AppendLine($"a=ice-ufrag:{media.IceUfrag}");
            sb.AppendLine($"a=ice-pwd:{media.IcePwd}");
            sb.AppendLine("a=ice-options:ice2");
            sb.AppendLine("a=setup:actpass");

            foreach (var formatPair in media.MediaFormats)
            {
                var format = formatPair.Value;
                sb.AppendLine($"a=rtpmap:{format.ID} {format.Name()}/{format.ClockRate()}");

                sb.AppendLine($"a=rtcp-fb:{format.ID} ccm fir");
                sb.AppendLine($"a=rtcp-fb:{format.ID} nack");
                sb.AppendLine($"a=rtcp-fb:{format.ID} nack pli");
                sb.AppendLine($"a=rtcp-fb:{format.ID} goog-remb");
                sb.AppendLine($"a=rtcp-fb:{format.ID} transport-cc");
                sb.AppendLine("a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time");
                sb.AppendLine("a=extmap:4 urn:ietf:params:rtp-hdrext:sdes:mid");
            }

            var firstSsrc = media.SsrcAttributes.FirstOrDefault();
            if (firstSsrc != null)
            {
                sb.AppendLine($"a=ssrc-group:FID {firstSsrc.SSRC}");
                sb.AppendLine($"a=msid:{firstSsrc.Cname} testv");
                sb.AppendLine($"a=ssrc:{firstSsrc.SSRC} cname:{firstSsrc.Cname}");
            }
        }

        foreach (var iceCandidate in sdp.IceCandidates ?? new List<string>())
        {
            sb.AppendLine($"a=candidate:{iceCandidate}");
        }

        foreach (var media in sdp.Media)
        {
            foreach (var iceCandidate in media.IceCandidates ?? new List<string>())
            {
                sb.AppendLine($"a=candidate:{iceCandidate}");
            }
        }

        sb.AppendLine("a=end-of-candidates");
        return sb.ToString();
    }
}
