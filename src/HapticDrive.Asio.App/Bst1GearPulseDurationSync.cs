using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal static class Bst1GearPulseDurationSync
{
    public const int DefaultGearDurationMs = 45;

    public static int NormalizeGearDuration(int durationMs, PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? SimagicPhprOutputDevice.DirectControlSafetyLimits;
        return Math.Clamp(durationMs, PhprUiValueConverter.MinimumDurationMs, safetyLimits.MaxDurationMs);
    }

    public static int ResolveSharedDuration(
        PHprRealGearPulseSettings brake,
        PHprRealGearPulseSettings throttle,
        PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? SimagicPhprOutputDevice.DirectControlSafetyLimits;
        var brakeDuration = brake.Normalize(safetyLimits).DurationMs;
        var throttleDuration = throttle.Normalize(safetyLimits).DurationMs;
        return NormalizeGearDuration(Math.Max(brakeDuration, throttleDuration), safetyLimits);
    }

    public static PHprRealGearPulseSettings WithSharedDuration(
        PHprRealGearPulseSettings settings,
        int sharedDurationMs,
        PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? SimagicPhprOutputDevice.DirectControlSafetyLimits;
        return settings.Normalize(safetyLimits) with
        {
            DurationMs = NormalizeGearDuration(sharedDurationMs, safetyLimits)
        };
    }

    public static int ResolveBst1Duration(
        bool syncToPhpr,
        int sharedPhprDurationMs,
        int customBst1DurationMs,
        PHprSafetyLimits? limits = null)
    {
        return NormalizeGearDuration(syncToPhpr ? sharedPhprDurationMs : customBst1DurationMs, limits);
    }
}
