using System.Text.Json;
using System.Text.Json.Serialization;

namespace HapticDrive.Simagic.PHPR.Research.Capture;

public static class SimagicCaptureJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
