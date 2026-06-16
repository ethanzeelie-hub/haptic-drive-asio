using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Simagic.PHPR.Abstractions.Routing;

public sealed record PaddleGearBenchTestResult(
    bool Accepted,
    string Message,
    WheelPaddleInputEvent PaddleEvent,
    PaddleGearBenchTestOptions Options,
    DateTimeOffset EvaluatedAtUtc,
    ShiftIntentEvent? ShiftIntentEvent = null,
    string? SuppressionReason = null)
{
    public static PaddleGearBenchTestResult AcceptedEvent(
        WheelPaddleInputEvent paddleEvent,
        PaddleGearBenchTestOptions options,
        ShiftIntentEvent shiftIntentEvent,
        DateTimeOffset evaluatedAtUtc)
    {
        return new PaddleGearBenchTestResult(
            true,
            $"Bench gear event accepted from {paddleEvent.PaddleSide} paddle for {options.OutputMode} P-HPR routing.",
            paddleEvent,
            options,
            evaluatedAtUtc,
            shiftIntentEvent);
    }

    public static PaddleGearBenchTestResult Suppressed(
        WheelPaddleInputEvent paddleEvent,
        PaddleGearBenchTestOptions options,
        string suppressionReason,
        DateTimeOffset evaluatedAtUtc)
    {
        return new PaddleGearBenchTestResult(
            false,
            $"Bench gear event suppressed: {suppressionReason}",
            paddleEvent,
            options,
            evaluatedAtUtc,
            SuppressionReason: suppressionReason);
    }
}
