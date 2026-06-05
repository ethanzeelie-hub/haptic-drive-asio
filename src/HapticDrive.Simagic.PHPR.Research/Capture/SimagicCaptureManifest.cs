namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureManifest
{
    public string Stage { get; init; } = "Stage 2H";

    public string Purpose { get; init; } = "Sanitized Simagic P700 / P-HPR capture metadata manifest for later Stage 2I analysis.";

    public IReadOnlyList<string> SafetyStatements { get; init; } =
    [
        "Metadata manifest only.",
        "No USB capture analysis.",
        "No raw capture bytes.",
        "No USB writes.",
        "No output reports.",
        "No feature reports.",
        "No vibration commands.",
        "No P-HPR commands.",
        "No SimPro Manager or SimHub control."
    ];

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public int SourceMetadataCount { get; init; }

    public IReadOnlyList<SimagicCaptureManifestEntry> Entries { get; init; } = [];

    public static SimagicCaptureManifest Create(IEnumerable<SimagicCaptureMetadata> metadataItems)
    {
        ArgumentNullException.ThrowIfNull(metadataItems);

        var validator = new SimagicCaptureMetadataValidator();
        var entries = metadataItems
            .Select(metadata => SimagicCaptureSanitizer.Sanitize(metadata))
            .Select(metadata => new SimagicCaptureManifestEntry
            {
                Metadata = metadata,
                Validation = validator.Validate(metadata)
            })
            .OrderBy(entry => entry.Metadata.CaptureStartedAtUtc)
            .ThenBy(entry => entry.Metadata.CaptureFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SimagicCaptureManifest
        {
            SourceMetadataCount = entries.Length,
            Entries = entries
        };
    }
}
