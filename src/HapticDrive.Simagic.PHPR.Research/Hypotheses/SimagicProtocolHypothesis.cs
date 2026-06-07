namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public sealed record SimagicProtocolHypothesis
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required SimagicProtocolFamily ProtocolFamily { get; init; }

    public required SimagicProtocolSource SoftwareSource { get; init; }

    public string TransportObservation { get; init; } = "Not applicable.";

    public string? PayloadPrefixHex { get; init; }

    public int? ReportLengthBytes { get; init; }

    public required string Summary { get; init; }

    public SimagicProtocolHypothesisConfidence Confidence { get; init; } =
        SimagicProtocolHypothesisConfidence.Unknown;

    public SimagicProtocolHypothesisStatus Status { get; init; } =
        SimagicProtocolHypothesisStatus.Hypothesis;

    public SimagicProtocolHypothesisStatus RealWriteStatus { get; init; } =
        SimagicProtocolHypothesisStatus.BlockedForRealWrite;

    public bool IsInputMapping { get; init; }

    public bool IsOutputCommand { get; init; }

    public bool MockOnly { get; init; } = true;

    public IReadOnlyList<SimagicProtocolHypothesisField> Fields { get; init; } = [];

    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];

    public IReadOnlyList<SimagicProtocolRisk> Risks { get; init; } = [];

    public IReadOnlyList<string> MissingData { get; init; } = [];

    public IReadOnlyList<string> ValidationNeeded { get; init; } = [];

    public string StageAllowedForNextAction { get; init; } =
        "Stage 2J documentation only.";

    public string NoWriteSafetyNote { get; init; } =
        "Nothing in this hypothesis authorises real USB writes.";
}
