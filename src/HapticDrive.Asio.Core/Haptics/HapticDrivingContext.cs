namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticDrivingContext(
    DrivingPhase DrivingPhase,
    PitState PitState,
    bool IsPaused,
    bool IsPlayerControlled,
    bool AllowsDrivingOutput);
