using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Shift;

public sealed record ShiftIntentEvaluationResult(
    bool WasAccepted,
    ShiftIntentEvent? ShiftIntentEvent,
    string? SuppressionReason,
    string Message,
    DrivingArmedState DrivingArmedStateAtEvaluation,
    WheelPaddleInputEvent PaddleEvent,
    ShiftIntentMode Mode,
    ShiftIntentDirection Direction,
    DateTimeOffset EvaluatedAtUtc)
{
    public static ShiftIntentEvaluationResult Accepted(
        ShiftIntentEvent shiftIntentEvent,
        WheelPaddleInputEvent paddleEvent,
        string message,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(shiftIntentEvent);
        ArgumentNullException.ThrowIfNull(paddleEvent);

        return new ShiftIntentEvaluationResult(
            true,
            shiftIntentEvent,
            null,
            message,
            shiftIntentEvent.DrivingArmedAtEvent,
            paddleEvent,
            shiftIntentEvent.Mode,
            shiftIntentEvent.Direction,
            evaluatedAtUtc);
    }

    public static ShiftIntentEvaluationResult Suppressed(
        WheelPaddleInputEvent paddleEvent,
        ShiftIntentMode mode,
        ShiftIntentDirection direction,
        DrivingArmedState drivingArmedState,
        string suppressionReason,
        string message,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(paddleEvent);

        return new ShiftIntentEvaluationResult(
            false,
            null,
            string.IsNullOrWhiteSpace(suppressionReason)
                ? "Shift intent was suppressed."
                : suppressionReason.Trim(),
            string.IsNullOrWhiteSpace(message)
                ? "Shift intent was suppressed."
                : message.Trim(),
            drivingArmedState,
            paddleEvent,
            mode,
            direction,
            evaluatedAtUtc);
    }
}
