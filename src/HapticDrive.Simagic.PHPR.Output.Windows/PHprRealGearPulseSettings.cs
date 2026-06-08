using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprRealGearPulseSettings
{
    public static PHprRealGearPulseSettings Default { get; } = new();

    public bool IsEnabled { get; init; } = true;

    public double Strength01 { get; init; } = 0.05d;

    public double FrequencyHz { get; init; } = 50d;

    public int DurationMs { get; init; } = 50;

    public PHprRealGearPulseSettings Normalize(PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? PHprSafetyLimits.Default;
        return this with
        {
            Strength01 = double.IsFinite(Strength01) ? Math.Clamp(Strength01, 0d, safetyLimits.MaxStrength01) : Default.Strength01,
            FrequencyHz = double.IsFinite(FrequencyHz) ? Math.Clamp(FrequencyHz, safetyLimits.MinFrequencyHz, safetyLimits.MaxFrequencyHz) : Default.FrequencyHz,
            DurationMs = Math.Clamp(DurationMs, 0, safetyLimits.MaxDurationMs)
        };
    }
}
