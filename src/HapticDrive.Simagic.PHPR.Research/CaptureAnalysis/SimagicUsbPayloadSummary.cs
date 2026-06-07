namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicUsbPayloadSummary
{
    public string PayloadFingerprint { get; init; } = "";

    public int PayloadLength { get; init; }

    public int Count { get; init; }

    public string PayloadPreviewHex { get; init; } = "";

    public IReadOnlyList<string> SourceFileNames { get; init; } = [];

    public IReadOnlyList<string> SourceColumns { get; init; } = [];

    public double? FirstTimestampSeconds { get; init; }

    public double? LastTimestampSeconds { get; init; }
}
