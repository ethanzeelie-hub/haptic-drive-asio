using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Actuation.Shift;

public sealed record ShiftIntentTelemetrySnapshot(
    int? LastKnownGear,
    int? LastKnownSpeedKph,
    int? LastKnownRpm,
    float? LastKnownSessionTime,
    uint? LastKnownFrameIdentifier,
    uint? LastKnownOverallFrameIdentifier,
    DateTimeOffset? LastVehicleStateUpdateAtUtc,
    TimeSpan? TelemetryAge)
{
    public static ShiftIntentTelemetrySnapshot None { get; } = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public static ShiftIntentTelemetrySnapshot FromPipelineSnapshot(HapticPipelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var telemetry = snapshot.VehicleState.Telemetry?.Value;
        return new ShiftIntentTelemetrySnapshot(
            telemetry is null ? null : telemetry.Gear,
            telemetry is null ? null : telemetry.SpeedKph,
            telemetry is null ? null : telemetry.EngineRpm,
            snapshot.VehicleState.Frame.SessionTime,
            snapshot.VehicleState.Frame.FrameIdentifier,
            snapshot.VehicleState.Frame.OverallFrameIdentifier,
            snapshot.LastVehicleStateUpdateAtUtc,
            snapshot.TelemetryAge);
    }
}
