namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureMetadata
{
    public string CaptureId { get; init; } = $"capture-{Guid.NewGuid():N}";

    public SimagicCaptureScenarioId? ScenarioId { get; init; }

    public string? ScenarioName { get; init; }

    public string? CaptureFileName { get; init; }

    public DateTimeOffset? CaptureStartedAtUtc { get; init; }

    public TimeSpan? CaptureDuration { get; init; }

    public SimagicCaptureSoftwareContext Software { get; init; } = new();

    public SimagicCaptureDeviceContext Device { get; init; } = new();

    public SimagicCaptureActionContext Action { get; init; } = new();

    public string? Notes { get; init; }

    public SimagicCaptureRedactionStatus RedactionStatus { get; init; } = SimagicCaptureRedactionStatus.NotReviewed;

    public bool ContainsSerialNumbers { get; init; }

    public bool ContainsPrivatePaths { get; init; }

    public string? RawCapturePath { get; init; }

    public string? SanitizedSummaryPath { get; init; }
}
