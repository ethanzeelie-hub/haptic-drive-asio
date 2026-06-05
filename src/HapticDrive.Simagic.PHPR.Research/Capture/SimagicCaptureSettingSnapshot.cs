namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed record SimagicCaptureSettingSnapshot
{
    public double? StrengthPercent { get; init; }

    public double? FrequencyHz { get; init; }

    public int? DurationMs { get; init; }
}
