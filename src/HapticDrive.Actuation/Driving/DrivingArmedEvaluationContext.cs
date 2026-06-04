namespace HapticDrive.Actuation.Driving;

public sealed record DrivingArmedEvaluationContext
{
    public bool HapticsRunning { get; init; } = true;

    public bool EmergencyMute { get; init; }

    public bool HasRecentTelemetry { get; init; }

    public DateTimeOffset? LastVehicleStateUpdateAtUtc { get; init; }

    public TimeSpan? TelemetryAge { get; init; }

    public bool TelemetryTimedOutMuted { get; init; }
}
