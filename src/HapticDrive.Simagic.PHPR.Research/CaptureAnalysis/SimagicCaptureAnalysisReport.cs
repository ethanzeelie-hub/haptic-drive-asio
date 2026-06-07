namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicCaptureAnalysisReport
{
    public string Stage { get; init; } = "Stage 2I";

    public string Purpose { get; init; } =
        "Read-only sanitized capture analysis summary for Simagic P700 / P-HPR research.";

    public IReadOnlyList<string> SafetyBoundary { get; init; } =
    [
        "No USB writes.",
        "No HID output reports.",
        "No HID feature reports.",
        "No vibration commands.",
        "No P-HPR commands.",
        "No protocol hypotheses.",
        "No raw capture bytes in exported JSON.",
        "No SimPro Manager / SimHub control."
    ];

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int SourceFileCount { get; init; }

    public int PayloadObservationCount { get; init; }

    public int UniquePayloadCount { get; init; }

    public IReadOnlyList<SimagicCaptureFileSummary> FileSummaries { get; init; } = [];

    public IReadOnlyList<SimagicUsbPayloadSummary> TopPayloads { get; init; } = [];

    public IReadOnlyList<SimagicPcapCaptureSummary> PcapSummaries { get; init; } = [];

    public IReadOnlyList<SimagicPayloadDiffObservation> DiffObservations { get; init; } = [];

    public IReadOnlyList<SimagicCaptureAnalysisWarning> Warnings { get; init; } = [];
}
