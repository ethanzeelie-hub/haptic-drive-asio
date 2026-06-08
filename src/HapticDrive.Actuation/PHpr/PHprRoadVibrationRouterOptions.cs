using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprRoadVibrationRouterOptions
{
    public static PHprRoadVibrationRouterOptions Disabled { get; } = new();

    public static PHprRoadVibrationRouterOptions EnabledDefault { get; } = new()
    {
        IsEnabled = true
    };

    public bool IsEnabled { get; init; }

    public PHprRoadVibrationPedalSettings Brake { get; init; } = PHprRoadVibrationPedalSettings.Default;

    public PHprRoadVibrationPedalSettings Throttle { get; init; } = PHprRoadVibrationPedalSettings.Default;

    public TimeSpan MinimumRouteInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    public int Priority { get; init; } = PHprPedalEffectProfile.RoadVibrationDefault.Priority;

    public PHprRoadVibrationRouterOptions Normalize(PHprSafetyLimits? limits = null)
    {
        return this with
        {
            Brake = (Brake ?? PHprRoadVibrationPedalSettings.Default).Normalize(limits),
            Throttle = (Throttle ?? PHprRoadVibrationPedalSettings.Default).Normalize(limits),
            MinimumRouteInterval = MinimumRouteInterval < TimeSpan.Zero ? TimeSpan.Zero : MinimumRouteInterval,
            Priority = Math.Clamp(Priority, 0, 1_000)
        };
    }
}
