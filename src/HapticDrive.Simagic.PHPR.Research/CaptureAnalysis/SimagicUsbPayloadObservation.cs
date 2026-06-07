using System.Text.Json.Serialization;

namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicUsbPayloadObservation
{
    public string SourceFileName { get; init; } = "";

    public string SourceColumn { get; init; } = "";

    public string? RecordKind { get; init; }

    public int? FrameNumber { get; init; }

    public double? TimestampSeconds { get; init; }

    public int PayloadLength { get; init; }

    public int Count { get; init; } = 1;

    public string PayloadFingerprint { get; init; } = "";

    public string PayloadPreviewHex { get; init; } = "";

    [JsonIgnore]
    public byte[] PayloadBytes { get; init; } = [];
}
