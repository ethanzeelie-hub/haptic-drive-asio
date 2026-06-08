using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprRoadVibrationPedalSettings
{
    public static PHprRoadVibrationPedalSettings Default { get; } = new();

    public bool IsEnabled { get; init; } = true;

    public double MinimumStrength01 { get; init; } = 0.01d;

    public double Strength01 { get; init; } = 0.04d;

    public double MinimumFrequencyHz { get; init; } = 25d;

    public double FrequencyHz { get; init; } = 45d;

    public int DurationMs { get; init; } = 50;

    public PHprRoadVibrationPedalSettings Normalize(PHprSafetyLimits? limits = null)
    {
        var safeLimits = limits ?? PHprSafetyLimits.Default;
        var defaultSettings = Default;
        var minimumStrength = SanitizeFinite(MinimumStrength01, defaultSettings.MinimumStrength01, 0d, safeLimits.MaxStrength01);
        var strength = SanitizeFinite(Strength01, defaultSettings.Strength01, 0d, safeLimits.MaxStrength01);
        var minimumFrequency = SanitizeFinite(MinimumFrequencyHz, defaultSettings.MinimumFrequencyHz, safeLimits.MinFrequencyHz, safeLimits.MaxFrequencyHz);
        var frequency = SanitizeFinite(FrequencyHz, defaultSettings.FrequencyHz, safeLimits.MinFrequencyHz, safeLimits.MaxFrequencyHz);

        return this with
        {
            MinimumStrength01 = Math.Min(minimumStrength, strength),
            Strength01 = Math.Max(minimumStrength, strength),
            MinimumFrequencyHz = Math.Min(minimumFrequency, frequency),
            FrequencyHz = Math.Max(minimumFrequency, frequency),
            DurationMs = Math.Clamp(DurationMs, 0, safeLimits.MaxDurationMs)
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
