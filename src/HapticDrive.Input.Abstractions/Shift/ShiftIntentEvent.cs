using HapticDrive.Input.Abstractions.Driving;

namespace HapticDrive.Input.Abstractions.Shift;

public sealed record ShiftIntentEvent(
    PaddleSide PaddleSide,
    DateTimeOffset TimestampUtc,
    DrivingArmedState DrivingArmedAtEvent,
    long SequenceNumber = 0,
    string? SourceDeviceId = null,
    int? LastTelemetryGear = null)
{
    public bool IsAcceptedByDrivingGate => DrivingArmedAtEvent.IsArmed;

    public static ShiftIntentEvent CreatePaddlePress(
        PaddleSide paddleSide,
        DrivingArmedState drivingArmedAtEvent,
        DateTimeOffset? timestampUtc = null,
        long sequenceNumber = 0,
        string? sourceDeviceId = null,
        int? lastTelemetryGear = null)
    {
        return new ShiftIntentEvent(
            paddleSide,
            timestampUtc ?? DateTimeOffset.UtcNow,
            drivingArmedAtEvent,
            sequenceNumber,
            sourceDeviceId,
            lastTelemetryGear);
    }
}
