using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Actuation.PHpr;

public enum PHprPedalEffectKind
{
    RoadVibration = 0,
    WheelSlip = 1,
    WheelLock = 2
}

public static class PHprPedalEffectKindExtensions
{
    public static PHprCommandSource ToCommandSource(this PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => PHprCommandSource.RoadTexture,
            PHprPedalEffectKind.WheelSlip => PHprCommandSource.WheelSlip,
            PHprPedalEffectKind.WheelLock => PHprCommandSource.WheelLock,
            _ => PHprCommandSource.RoadTexture
        };
    }
}
