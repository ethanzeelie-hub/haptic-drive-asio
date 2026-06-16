using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprSlipLockEffectSettings
{
    public const int MinimumContinuousDurationMs = 100;
    public const int DefaultContinuousDurationMs = 120;

    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Throttle;

    public double MinimumStrength01 { get; init; } = 0.03d;

    public double Strength01 { get; init; } = 0.08d;

    public double MinimumFrequencyHz { get; init; } = 45d;

    public double FrequencyHz { get; init; } = 50d;

    public int DurationMs { get; init; } = DefaultContinuousDurationMs;

    public int Priority { get; init; } = PHprPedalEffectProfile.WheelSlipDefault.Priority;

    public static PHprSlipLockEffectSettings DefaultFor(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.WheelLock => new PHprSlipLockEffectSettings
            {
                TargetModule = PHprGearPulseTarget.Brake,
                MinimumStrength01 = 0.04d,
                Strength01 = 0.10d,
                MinimumFrequencyHz = 50d,
                FrequencyHz = 50d,
                DurationMs = DefaultContinuousDurationMs,
                Priority = PHprPedalEffectProfile.WheelLockDefault.Priority
            },
            _ => new PHprSlipLockEffectSettings
            {
                TargetModule = PHprGearPulseTarget.Throttle,
                MinimumStrength01 = 0.03d,
                Strength01 = 0.08d,
                MinimumFrequencyHz = 45d,
                FrequencyHz = 50d,
                DurationMs = DefaultContinuousDurationMs,
                Priority = PHprPedalEffectProfile.WheelSlipDefault.Priority
            }
        };
    }

    public PHprSlipLockEffectSettings Normalize(
        PHprPedalEffectKind kind,
        PHprSafetyLimits? limits = null)
    {
        var defaults = DefaultFor(kind);
        var safeLimits = limits ?? PHprSafetyLimits.Default;
        var target = Enum.IsDefined(TargetModule)
            ? TargetModule
            : defaults.TargetModule;
        var minimumStrength = SanitizeFinite(MinimumStrength01, defaults.MinimumStrength01, 0d, safeLimits.MaxStrength01);
        var strength = SanitizeFinite(Strength01, defaults.Strength01, 0d, safeLimits.MaxStrength01);
        var minimumFrequency = SanitizeFinite(MinimumFrequencyHz, defaults.MinimumFrequencyHz, safeLimits.MinFrequencyHz, safeLimits.MaxFrequencyHz);
        var frequency = SanitizeFinite(FrequencyHz, defaults.FrequencyHz, safeLimits.MinFrequencyHz, safeLimits.MaxFrequencyHz);

        return this with
        {
            TargetModule = target,
            MinimumStrength01 = Math.Min(minimumStrength, strength),
            Strength01 = Math.Max(minimumStrength, strength),
            MinimumFrequencyHz = Math.Min(minimumFrequency, frequency),
            FrequencyHz = Math.Max(minimumFrequency, frequency),
            DurationMs = Math.Clamp(DurationMs, MinimumContinuousDurationMs, safeLimits.MaxDurationMs),
            Priority = Math.Clamp(Priority, 0, 1_000)
        };
    }

    public double ScaleStrength(double intensity01)
    {
        return Lerp(MinimumStrength01, Strength01, intensity01);
    }

    public double ScaleFrequency(double intensity01)
    {
        return Lerp(MinimumFrequencyHz, FrequencyHz, intensity01);
    }

    private static double Lerp(double minimum, double maximum, double intensity01)
    {
        var amount = double.IsFinite(intensity01) ? Math.Clamp(intensity01, 0d, 1d) : 0d;
        return minimum + ((maximum - minimum) * amount);
    }

    private static double SanitizeFinite(double value, double fallback, double minimum, double maximum)
    {
        return double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }
}
