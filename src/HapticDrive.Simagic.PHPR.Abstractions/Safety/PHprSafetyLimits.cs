namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public sealed record PHprSafetyLimits(
    double MaxStrength01,
    int MaxDurationMs,
    double MinFrequencyHz,
    double MaxFrequencyHz,
    int MaxCommandsPerSecond,
    int MaxContinuousDurationMs,
    bool AllowRealDeviceWrites)
{
    public static PHprSafetyLimits Default { get; } = new(
        MaxStrength01: 0.10d,
        MaxDurationMs: 100,
        MinFrequencyHz: 5d,
        MaxFrequencyHz: 250d,
        MaxCommandsPerSecond: 10,
        MaxContinuousDurationMs: 500,
        AllowRealDeviceWrites: false);
}
