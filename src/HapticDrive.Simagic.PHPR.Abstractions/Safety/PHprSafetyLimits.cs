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
        MaxStrength01: 1.0d,
        MaxDurationMs: 1_000,
        MinFrequencyHz: 1d,
        MaxFrequencyHz: 50d,
        MaxCommandsPerSecond: 10,
        MaxContinuousDurationMs: 1_000,
        AllowRealDeviceWrites: false);
}
