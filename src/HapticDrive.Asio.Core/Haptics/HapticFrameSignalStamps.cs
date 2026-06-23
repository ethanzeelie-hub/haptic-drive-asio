namespace HapticDrive.Asio.Core.Haptics;

public readonly record struct HapticFrameSignalStamps(
    HapticSignalStamp? Telemetry,
    HapticSignalStamp? Motion,
    HapticSignalStamp? Session,
    HapticSignalStamp? Lap,
    HapticSignalStamp? Participant,
    HapticSignalStamp? CarStatus,
    HapticSignalStamp? Damage,
    HapticSignalStamp? MotionEx,
    HapticSignalStamp? Event);
