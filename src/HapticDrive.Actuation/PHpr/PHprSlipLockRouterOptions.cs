using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprSlipLockRouterOptions
{
    public static PHprSlipLockRouterOptions Disabled { get; } = new();

    public static PHprSlipLockRouterOptions EnabledDefault { get; } = new()
    {
        IsEnabled = true
    };

    public bool IsEnabled { get; init; }

    public PHprSlipLockEffectSettings WheelSlip { get; init; } =
        PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelSlip);

    public PHprSlipLockEffectSettings WheelLock { get; init; } =
        PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelLock);

    public TimeSpan MinimumRouteInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    public TimeSpan HoldTimeout { get; init; } = TimeSpan.FromMilliseconds(350);

    public PHprSlipLockRouterOptions Normalize(PHprSafetyLimits? limits = null)
    {
        return this with
        {
            WheelSlip = (WheelSlip ?? PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelSlip))
                .Normalize(PHprPedalEffectKind.WheelSlip, limits),
            WheelLock = (WheelLock ?? PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelLock))
                .Normalize(PHprPedalEffectKind.WheelLock, limits),
            MinimumRouteInterval = MinimumRouteInterval < TimeSpan.Zero
                ? TimeSpan.Zero
                : MinimumRouteInterval,
            HoldTimeout = HoldTimeout < TimeSpan.Zero
                ? TimeSpan.Zero
                : HoldTimeout
        };
    }

    public PHprSlipLockEffectSettings GetSettings(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.WheelSlip => WheelSlip,
            PHprPedalEffectKind.WheelLock => WheelLock,
            _ => WheelSlip
        };
    }
}
