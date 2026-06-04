namespace HapticDrive.Actuation.Driving;

public sealed record DrivingArmedStateServiceOptions
{
    public static DrivingArmedStateServiceOptions Default { get; } = new();

    public bool MenuSafeModeEnabled { get; init; } = true;

    public bool RequireRecentTelemetry { get; init; } = true;

    public TimeSpan TelemetryFreshnessThreshold { get; init; } = TimeSpan.FromMilliseconds(250);

    public bool AllowZeroSpeedActiveDriving { get; init; } = true;

    public bool DiagnosticsOnlyUnsafeOverride { get; init; }

    public DrivingArmedStateServiceOptions Normalize()
    {
        return TelemetryFreshnessThreshold > TimeSpan.Zero
            ? this
            : this with { TelemetryFreshnessThreshold = Default.TelemetryFreshnessThreshold };
    }
}
