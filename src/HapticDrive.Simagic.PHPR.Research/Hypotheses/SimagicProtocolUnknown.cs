namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public sealed record SimagicProtocolUnknown
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public bool BlocksRealWrite { get; init; } = true;

    public IReadOnlyList<string> EvidenceNeeded { get; init; } = [];
}
