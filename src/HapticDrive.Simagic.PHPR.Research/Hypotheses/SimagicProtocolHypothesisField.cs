namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public sealed record SimagicProtocolHypothesisField
{
    public required string CandidateFieldName { get; init; }

    public int? ByteOffset { get; init; }

    public int? ByteLength { get; init; }

    public string Encoding { get; init; } = "unknown";

    public IReadOnlyList<string> ObservedValues { get; init; } = [];

    public string Interpretation { get; init; } = "Unknown.";

    public SimagicProtocolHypothesisConfidence Confidence { get; init; } =
        SimagicProtocolHypothesisConfidence.Unknown;

    public IReadOnlyList<string> Notes { get; init; } = [];
}
