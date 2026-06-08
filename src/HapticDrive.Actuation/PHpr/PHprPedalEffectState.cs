namespace HapticDrive.Actuation.PHpr;

public sealed record PHprPedalEffectState
{
    public bool IsEnabled { get; init; } = true;

    public PHprGearPulseTarget TargetModule { get; init; } = PHprGearPulseTarget.Both;

    public PHprPedalEffectProfile Profile { get; init; } = PHprPedalEffectProfile.RoadVibrationDefault;

    public static PHprPedalEffectState DefaultFor(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => new PHprPedalEffectState
            {
                TargetModule = PHprGearPulseTarget.Both,
                Profile = PHprPedalEffectProfile.RoadVibrationDefault
            },
            PHprPedalEffectKind.WheelSlip => new PHprPedalEffectState
            {
                TargetModule = PHprGearPulseTarget.Throttle,
                Profile = PHprPedalEffectProfile.WheelSlipDefault
            },
            PHprPedalEffectKind.WheelLock => new PHprPedalEffectState
            {
                TargetModule = PHprGearPulseTarget.Brake,
                Profile = PHprPedalEffectProfile.WheelLockDefault
            },
            _ => new PHprPedalEffectState()
        };
    }

    public PHprPedalEffectState Normalize(PHprPedalEffectKind kind)
    {
        var defaults = DefaultFor(kind);
        var target = Enum.IsDefined(TargetModule)
            ? TargetModule
            : defaults.TargetModule;

        return this with
        {
            TargetModule = target,
            Profile = (Profile ?? defaults.Profile).Normalize(kind)
        };
    }
}
