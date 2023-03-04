using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bld.RtpToWebRtcRestreamer.RtpNg;
internal static class Constants
{
    public const int MAX_UDP_SIZE = 0x10000;

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
