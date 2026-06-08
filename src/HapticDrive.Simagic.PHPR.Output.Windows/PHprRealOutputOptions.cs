using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprRealOutputOptions
{
    public static PHprRealOutputOptions Disabled { get; } = new();

    public bool DirectControlEnabled { get; init; }

    public bool DirectControlArmed { get; init; }

    public PHprHidDeviceSelector Selector { get; init; } = PHprHidDeviceSelector.None;

    public PHprRealGearPulseSettings BrakeGearPulse { get; init; } = PHprRealGearPulseSettings.Default;

    public PHprRealGearPulseSettings ThrottleGearPulse { get; init; } = PHprRealGearPulseSettings.Default;

    public PHprRealOutputOptions Normalize(PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? PHprSafetyLimits.Default;
        return this with
        {
            Selector = (Selector ?? PHprHidDeviceSelector.None).Normalize(),
            BrakeGearPulse = (BrakeGearPulse ?? PHprRealGearPulseSettings.Default).Normalize(safetyLimits),
            ThrottleGearPulse = (ThrottleGearPulse ?? PHprRealGearPulseSettings.Default).Normalize(safetyLimits)
        };
    }
}
