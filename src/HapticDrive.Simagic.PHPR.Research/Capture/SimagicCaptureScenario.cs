namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureScenario
{
    public required SimagicCaptureScenarioId Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string SoftwareUnderTest { get; init; }

    public required string DeviceName { get; init; }

    public required SimagicCaptureTargetModule RecommendedTarget { get; init; }

    public required string Description { get; init; }

    public bool RequiresStrength { get; init; }

    public bool RequiresFrequency { get; init; }

    public bool RequiresDuration { get; init; }
}
