namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureActionContext
{
    public string? ActionPerformed { get; init; }

    public SimagicCaptureSettingSnapshot SettingBefore { get; init; } = new();

    public SimagicCaptureSettingSnapshot SettingAfter { get; init; } = new();

    public bool? ExpectedVibrationObserved { get; init; }

    public string? ActualObservedBehaviour { get; init; }
}
