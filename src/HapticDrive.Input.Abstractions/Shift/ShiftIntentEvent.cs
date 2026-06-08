using HapticDrive.Input.Abstractions.Driving;

namespace HapticDrive.Input.Abstractions.Shift;

public sealed record ShiftIntentEvent(
    PaddleSide PaddleSide,
    DateTimeOffset TimestampUtc,
    DrivingArmedState DrivingArmedAtEvent,
    long SequenceNumber = 0,
    string? SourceDeviceId = null,
    int? LastTelemetryGear = null,
    ShiftIntentDirection Direction = ShiftIntentDirection.Unknown,
    ShiftIntentSource Source = ShiftIntentSource.WheelPaddle,
    ShiftIntentMode Mode = ShiftIntentMode.InstantPaddleOnly,
    long? StopwatchTicks = null,
    int? SourceButtonId = null,
    int? LastKnownSpeedKph = null,
    int? LastKnownRpm = null,
    float? LastKnownSessionTime = null,
    uint? LastKnownFrameIdentifier = null,
    Guid? CorrelationId = null,
    DateTimeOffset? AcceptedAtUtc = null)
{
    public bool IsAcceptedByDrivingGate => DrivingArmedAtEvent.IsArmed;

    public static ShiftIntentEvent CreatePaddlePress(
        PaddleSide paddleSide,
        DrivingArmedState drivingArmedAtEvent,
        DateTimeOffset? timestampUtc = null,
        long sequenceNumber = 0,
        string? sourceDeviceId = null,
        int? lastTelemetryGear = null,
        ShiftIntentDirection direction = ShiftIntentDirection.Unknown,
        ShiftIntentSource source = ShiftIntentSource.WheelPaddle,
        ShiftIntentMode mode = ShiftIntentMode.InstantPaddleOnly,
        long? stopwatchTicks = null,
        int? sourceButtonId = null,
        int? lastKnownSpeedKph = null,
        int? lastKnownRpm = null,
        float? lastKnownSessionTime = null,
        uint? lastKnownFrameIdentifier = null,
        Guid? correlationId = null,
        DateTimeOffset? acceptedAtUtc = null)
    {
        return new ShiftIntentEvent(
            paddleSide,
            timestampUtc ?? DateTimeOffset.UtcNow,
            drivingArmedAtEvent,
            sequenceNumber,
            sourceDeviceId,
            lastTelemetryGear,
            NormalizeDirection(paddleSide, direction),
            source,
            mode,
            stopwatchTicks,
            sourceButtonId,
            lastKnownSpeedKph,
            lastKnownRpm,
            lastKnownSessionTime,
            lastKnownFrameIdentifier,
            correlationId ?? Guid.NewGuid(),
            acceptedAtUtc);
    }

    public static ShiftIntentDirection DirectionForPaddle(PaddleSide paddleSide)
    {
        return paddleSide switch
        {
            PaddleSide.Left => ShiftIntentDirection.Downshift,
            PaddleSide.Right => ShiftIntentDirection.Upshift,
            _ => ShiftIntentDirection.Unknown
        };
    }

    private static ShiftIntentDirection NormalizeDirection(
        PaddleSide paddleSide,
        ShiftIntentDirection direction)
    {
        return direction == ShiftIntentDirection.Unknown
            ? DirectionForPaddle(paddleSide)
            : direction;
    }
}
