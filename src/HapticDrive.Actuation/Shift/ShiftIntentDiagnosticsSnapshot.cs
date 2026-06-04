using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Shift;

public sealed record ShiftIntentDiagnosticsSnapshot(
    bool IsEnabled,
    ShiftIntentMode Mode,
    long TotalPaddleEventsObserved,
    long AcceptedShiftIntentCount,
    long SuppressedShiftIntentCount,
    PaddleSide LastPaddleSide,
    ShiftIntentDirection LastDirection,
    WheelPaddleInputEvent? LastPaddleEvent,
    ShiftIntentEvent? LastAcceptedEvent,
    ShiftIntentEvaluationResult? LastSuppressedEvent,
    string? LastSuppressionReason,
    DrivingArmedState LastDrivingArmedState,
    ShiftIntentTelemetrySnapshot LastTelemetry,
    long PendingConfirmationCount,
    string? LastError);
