namespace HapticDrive.Simagic.PHPR.Abstractions.Commands;

[Flags]
public enum PHprSafetyFlags
{
    None = 0,
    MockOnly = 1 << 0,
    ClampedStrength = 1 << 1,
    ClampedFrequency = 1 << 2,
    ClampedDuration = 1 << 3,
    EmergencyStop = 1 << 4,
    Rejected = 1 << 5
}
