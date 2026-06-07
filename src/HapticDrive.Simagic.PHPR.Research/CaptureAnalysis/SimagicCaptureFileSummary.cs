namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed record SimagicCaptureFileSummary
{
    public string SourceFileName { get; init; } = "";

    public SimagicCaptureAnalysisSourceKind SourceKind { get; init; }

    public int PayloadRecordCount { get; init; }

    public int? DeclaredPayloadRecordCount { get; init; }

    public int? DeclaredSetReportCandidateCount { get; init; }

    public IReadOnlyDictionary<string, int> PayloadColumnCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<int, int> PayloadLengthCounts { get; init; } =
        new Dictionary<int, int>();
}
