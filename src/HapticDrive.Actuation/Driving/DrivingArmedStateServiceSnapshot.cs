using HapticDrive.Input.Abstractions.Driving;

namespace HapticDrive.Actuation.Driving;

public sealed record DrivingArmedStateServiceSnapshot(
    DrivingArmedState Current,
    DrivingArmedSuppressionReason LastSuppressionReason,
    DateTimeOffset LastEvaluatedAtUtc,
    TimeSpan? LastTelemetryAge,
    bool MenuSafeModeEnabled,
    bool RequireRecentTelemetry,
    TimeSpan TelemetryFreshnessThreshold,
    bool AllowZeroSpeedActiveDriving,
    bool DiagnosticsOnlyUnsafeOverride);
