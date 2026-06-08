using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprSlipLockEffectSettings
{
    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Throttle;

    public double MinimumStrength01 { get; init; } = 0.03d;

    public double Strength01 { get; init; } = 0.08d;

    public double MinimumFrequencyHz { get; init; } = 45d;

    public double FrequencyHz { get; init; } = 75d;

    public int DurationMs { get; init; } = 50;

    public int Priority { get; init; } = PHprPedalEffectProfile.WheelSlipDefault.Priority;

    public static PHprSlipLockEffectSettings DefaultFor(PHprPedalEffectKind kind)
    {
        var state = PHprPedalEffectState.DefaultFor(kind);
        var profile = state.Profile;
        return new PHprSlipLockEffectSettings
        {
            TargetModule = state.TargetModule,
            MinimumStrength01 = profile.MinimumStrength01,
            Strength01 = profile.Strength01,
            MinimumFrequencyHz = profile.MinimumFrequencyHz,
            FrequencyHz = profile.FrequencyHz,
            DurationMs = profile.DurationMs,
            Priority = profile.Priority
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
            DurationMs = Math.Clamp(DurationMs, 10, safeLimits.MaxDurationMs),
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
