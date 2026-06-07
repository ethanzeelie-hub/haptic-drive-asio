namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicPayloadDiffObservation
{
    public string LeftSource { get; init; } = "";

    public string RightSource { get; init; } = "";

    public string LeftFingerprint { get; init; } = "";

    public string RightFingerprint { get; init; } = "";

    public int PayloadLength { get; init; }

    public int ChangedByteCount { get; init; }

    public string LeftPayloadPreviewHex { get; init; } = "";

    public string RightPayloadPreviewHex { get; init; } = "";

    public IReadOnlyList<SimagicPayloadByteDifference> Differences { get; init; } = [];
}
