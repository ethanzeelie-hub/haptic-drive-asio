using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Commands;

public sealed record PHprCommand(
    PHprModuleId TargetModule,
    double Strength01,
    double FrequencyHz,
    int DurationMs,
    PHprCommandSource Source,
    DateTimeOffset TimestampUtc,
    int Priority = 0,
    PHprSafetyFlags SafetyFlags = PHprSafetyFlags.None)
{
    public static PHprCommand Create(
        PHprModuleId targetModule,
        double strength01,
        double frequencyHz,
        int durationMs,
        PHprCommandSource source,
        int priority = 0,
        DateTimeOffset? timestampUtc = null,
        PHprSafetyFlags safetyFlags = PHprSafetyFlags.None)
    {
        return new PHprCommand(
            targetModule,
            strength01,
            frequencyHz,
            durationMs,
            source,
            timestampUtc ?? DateTimeOffset.UtcNow,
            priority,
            safetyFlags);
    }

    public PHprCommand ClampTo(PHprSafetyLimits safetyLimits)
    {
        var flags = SafetyFlags;
        var strength = ClampFinite(Strength01, 0d, safetyLimits.MaxStrength01, 0d);
        if (!NearlyEqual(strength, Strength01))
        {
            flags |= PHprSafetyFlags.ClampedStrength;
        }

        var frequency = ClampFinite(FrequencyHz, safetyLimits.MinFrequencyHz, safetyLimits.MaxFrequencyHz, safetyLimits.MinFrequencyHz);
        if (!NearlyEqual(frequency, FrequencyHz))
        {
            flags |= PHprSafetyFlags.ClampedFrequency;
        }

        var duration = DurationMs < 0 ? 0 : Math.Min(DurationMs, safetyLimits.MaxDurationMs);
        if (duration != DurationMs)
        {
            flags |= PHprSafetyFlags.ClampedDuration;
        }

        return this with
        {
            Strength01 = strength,
            FrequencyHz = frequency,
            DurationMs = duration,
            SafetyFlags = flags
        };
    }

    private static double ClampFinite(double value, double min, double max, double fallback)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static bool NearlyEqual(double left, double right)
    {
        return Math.Abs(left - right) < 0.000_001d;
    }
}
