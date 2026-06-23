namespace HapticDrive.Asio.Core.Haptics;

[Flags]
public enum HapticSignalKind
{
    None = 0,
    Telemetry = 1,
    Motion = 2,
    Session = 4,
    Lap = 8,
    CarStatus = 16,
    Damage = 32,
    MotionEx = 64,
    Event = 128
}
