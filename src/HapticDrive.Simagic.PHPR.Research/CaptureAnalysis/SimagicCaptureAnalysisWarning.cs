namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicCaptureAnalysisWarning
{
    public string SourceFileName { get; init; } = "";

    public string Message { get; init; } = "";
}
