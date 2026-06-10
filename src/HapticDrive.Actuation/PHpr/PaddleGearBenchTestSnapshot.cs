using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.PHpr;

public sealed record PaddleGearBenchTestSnapshot(
    PaddleGearBenchTestOptions Options,
    long AcceptedBenchGearEventCount,
    long SuppressedBenchGearEventCount,
    long LeftPaddleAcceptedCount,
    long RightPaddleAcceptedCount,
    ShiftIntentEvent? LastAcceptedBenchEvent,
    WheelPaddleInputEvent? LastPaddleEvent,
    string? LastSuppressionReason,
    PaddleGearBenchTestResult? LastResult,
    string? LastOutputStatus,
    string? LastError)
{
    public bool IsEnabled => Options.IsEnabled;

    public bool IsArmed => Options.IsArmed;

    public PaddleGearBenchTestOutputMode OutputMode => Options.OutputMode;
}
