namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public sealed record SimagicProtocolHypothesisSet
{
    public string Stage { get; init; } = "Stage 2J";

    public string Purpose { get; init; } =
        "Formal P-HPR protocol hypotheses derived from sanitized Stage 2I evidence and related input evidence.";

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> SafetyBoundary { get; init; } =
    [
        "Hypotheses only.",
        "No USB writes.",
        "No HID output reports.",
        "No HID feature reports.",
        "No vibration commands.",
        "No production protocol adapter.",
        "No protocol encoder or decoder for live hardware.",
        "No PHprCommand creation.",
        "No IPHprOutputDevice or MockPhprOutputDevice calls.",
        "No ShiftIntentEvent to haptic output routing.",
        "Nothing in this document authorises real USB writes."
    ];

    public IReadOnlyDictionary<string, string> ConfidenceScale { get; init; } =
        new Dictionary<string, string>
        {
            ["Unknown"] = "Not enough evidence to assign meaning.",
            ["Low"] = "Possible interpretation with limited or unresolved evidence.",
            ["Medium"] = "Repeated observation, but still missing key validation.",
            ["High"] = "Repeated observation matching controlled scenario changes.",
            ["ConfirmedObservation"] = "Bytes or input bits were directly observed in sanitized captures."
        };

    public IReadOnlyList<string> EvidenceSourcesReviewed { get; init; } = [];

    public IReadOnlyList<SimagicProtocolHypothesis> Hypotheses { get; init; } = [];

    public IReadOnlyList<SimagicProtocolUnknown> Unknowns { get; init; } = [];

    public IReadOnlyList<string> Stage2KAllowedMockSurface { get; init; } = [];

    public IReadOnlyList<string> RealWriteBlockers { get; init; } = [];

    public IReadOnlyList<string> OptionalUserData { get; init; } = [];
}
