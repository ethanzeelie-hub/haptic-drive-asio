using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

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

    public static ShiftIntentTelemetrySnapshot FromVehicleState(
        VehicleState vehicleState,
        DateTimeOffset? lastVehicleStateUpdateAtUtc = null,
        TimeSpan? telemetryAge = null)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        var telemetry = vehicleState.Telemetry?.Value;
        return new ShiftIntentTelemetrySnapshot(
            telemetry is null ? null : telemetry.Gear,
            telemetry is null ? null : telemetry.SpeedKph,
            telemetry is null ? null : telemetry.EngineRpm,
            vehicleState.Frame.SessionTime,
            vehicleState.Frame.FrameIdentifier,
            vehicleState.Frame.OverallFrameIdentifier,
            lastVehicleStateUpdateAtUtc,
            telemetryAge);
    }

    public static ShiftIntentTelemetrySnapshot FromHapticFrame(
        HapticFrame frame,
        VehicleState vehicleState,
        DateTimeOffset? lastVehicleStateUpdateAtUtc = null,
        TimeSpan? telemetryAge = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(vehicleState);

        var fallback = FromVehicleState(vehicleState, lastVehicleStateUpdateAtUtc, telemetryAge);
        var speedKph = frame.Signals.SpeedMetersPerSecond is null
            ? fallback.LastKnownSpeedKph
            : (int)Math.Round(frame.Signals.SpeedMetersPerSecond.Value * 3.6f, MidpointRounding.AwayFromZero);

        return fallback with
        {
            LastKnownGear = frame.Signals.Gear ?? fallback.LastKnownGear,
            LastKnownSpeedKph = speedKph,
            LastKnownRpm = frame.Signals.EngineRpm ?? fallback.LastKnownRpm,
            LastKnownSessionTime = vehicleState.Frame.SessionTime,
            LastKnownFrameIdentifier = vehicleState.Frame.FrameIdentifier,
            LastKnownOverallFrameIdentifier = vehicleState.Frame.OverallFrameIdentifier
        };
    }
}
