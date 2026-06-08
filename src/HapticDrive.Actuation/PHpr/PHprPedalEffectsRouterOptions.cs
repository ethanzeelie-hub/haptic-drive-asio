namespace HapticDrive.Actuation.PHpr;

public sealed record PHprPedalEffectsRouterOptions
{
    public static PHprPedalEffectsRouterOptions Default { get; } = new();

    public bool IsEnabled { get; init; } = true;

    public PHprPedalEffectState RoadVibration { get; init; } =
        PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.RoadVibration);

    public PHprPedalEffectState WheelSlip { get; init; } =
        PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelSlip);

    public PHprPedalEffectState WheelLock { get; init; } =
        PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelLock);

    public TimeSpan MinimumRouteInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    public PHprPedalEffectsRouterOptions Normalize()
    {
        return this with
        {
            RoadVibration = (RoadVibration ?? PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.RoadVibration))
                .Normalize(PHprPedalEffectKind.RoadVibration),
            WheelSlip = (WheelSlip ?? PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelSlip))
                .Normalize(PHprPedalEffectKind.WheelSlip),
            WheelLock = (WheelLock ?? PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelLock))
                .Normalize(PHprPedalEffectKind.WheelLock),
            MinimumRouteInterval = MinimumRouteInterval < TimeSpan.Zero
                ? TimeSpan.Zero
                : MinimumRouteInterval
        };
    }

    public PHprPedalEffectState GetState(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => RoadVibration,
            PHprPedalEffectKind.WheelSlip => WheelSlip,
            PHprPedalEffectKind.WheelLock => WheelLock,
            _ => RoadVibration
        };
    }
}
