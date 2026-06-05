namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureManifestEntry
{
    public required SimagicCaptureMetadata Metadata { get; init; }

    public SimagicCaptureValidationResult Validation { get; init; } = new();
}
