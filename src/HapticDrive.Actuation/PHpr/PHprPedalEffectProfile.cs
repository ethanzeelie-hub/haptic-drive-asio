namespace HapticDrive.Actuation.PHpr;

public sealed record PHprPedalEffectProfile
{
    public static PHprPedalEffectProfile RoadVibrationDefault { get; } = new()
    {
        MinimumStrength01 = 0.01d,
        Strength01 = 0.04d,
        MinimumFrequencyHz = 25d,
        FrequencyHz = 45d,
        DurationMs = 50,
        Priority = 10
    };

    public static PHprPedalEffectProfile WheelSlipDefault { get; } = new()
    {
        MinimumStrength01 = 0.03d,
        Strength01 = 0.08d,
        MinimumFrequencyHz = 35d,
        FrequencyHz = 50d,
        DurationMs = 50,
        Priority = 50
    };

    public static PHprPedalEffectProfile WheelLockDefault { get; } = new()
    {
        MinimumStrength01 = 0.04d,
        Strength01 = 0.10d,
        MinimumFrequencyHz = 40d,
        FrequencyHz = 50d,
        DurationMs = 50,
        Priority = 75
    };

    public double MinimumStrength01 { get; init; }

    public double Strength01 { get; init; }

    public double MinimumFrequencyHz { get; init; }

    public double FrequencyHz { get; init; }

    public int DurationMs { get; init; }

    public int Priority { get; init; }

    public static PHprPedalEffectProfile DefaultFor(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => RoadVibrationDefault,
            PHprPedalEffectKind.WheelSlip => WheelSlipDefault,
            PHprPedalEffectKind.WheelLock => WheelLockDefault,
            _ => RoadVibrationDefault
        };
    }

    public PHprPedalEffectProfile Normalize(PHprPedalEffectKind kind)
    {
        var defaults = DefaultFor(kind);
        var minimumStrength = SanitizeFinite(MinimumStrength01, defaults.MinimumStrength01, 0d, 1d);
        var strength = SanitizeFinite(Strength01, defaults.Strength01, 0d, 1d);
        var minimumFrequency = SanitizeFinite(MinimumFrequencyHz, defaults.MinimumFrequencyHz, 1d, 50d);
        var frequency = SanitizeFinite(FrequencyHz, defaults.FrequencyHz, 1d, 50d);

        return this with
        {
            MinimumStrength01 = Math.Min(minimumStrength, strength),
            Strength01 = Math.Max(minimumStrength, strength),
            MinimumFrequencyHz = Math.Min(minimumFrequency, frequency),
            FrequencyHz = Math.Max(minimumFrequency, frequency),
            DurationMs = Math.Clamp(DurationMs, 10, 1_000),
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
