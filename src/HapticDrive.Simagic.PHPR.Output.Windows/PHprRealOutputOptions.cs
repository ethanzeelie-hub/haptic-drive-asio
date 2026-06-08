using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprRealOutputOptions
{
    public static PHprRealOutputOptions Disabled { get; } = new();

    public const int MinWriteTimeoutMs = 25;

    public const int MaxWriteTimeoutMs = 2_000;

    public const int DefaultWriteTimeoutMs = 250;

    public bool DirectControlEnabled { get; init; }

    public bool DirectControlArmed { get; init; }

    public int WriteTimeoutMs { get; init; } = DefaultWriteTimeoutMs;

    public PHprHidDeviceSelector Selector { get; init; } = PHprHidDeviceSelector.None;

    public PHprRealGearPulseSettings BrakeGearPulse { get; init; } = PHprRealGearPulseSettings.Default;

    public PHprRealGearPulseSettings ThrottleGearPulse { get; init; } = PHprRealGearPulseSettings.Default;

    public PHprRealOutputOptions Normalize(PHprSafetyLimits? limits = null)
    {
        var safetyLimits = limits ?? PHprSafetyLimits.Default;
        return this with
        {
            WriteTimeoutMs = Math.Clamp(WriteTimeoutMs, MinWriteTimeoutMs, MaxWriteTimeoutMs),
            Selector = (Selector ?? PHprHidDeviceSelector.None).Normalize(),
            BrakeGearPulse = (BrakeGearPulse ?? PHprRealGearPulseSettings.Default).Normalize(safetyLimits),
            ThrottleGearPulse = (ThrottleGearPulse ?? PHprRealGearPulseSettings.Default).Normalize(safetyLimits)
        };
    }
}
