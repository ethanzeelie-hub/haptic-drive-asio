namespace HapticDrive.Asio.Core.Vehicle.Freshness;

public sealed record TelemetryFreshnessPolicy(
    TimeSpan MaxTelemetryAge,
    TimeSpan MaxMotionAge,
    TimeSpan MaxSessionAge,
    TimeSpan MaxLapAge,
    TimeSpan MaxStatusAge,
    uint MaxFrameLag)
{
    public static TelemetryFreshnessPolicy Default { get; } = new(
        MaxTelemetryAge: TimeSpan.FromMilliseconds(250),
        MaxMotionAge: TimeSpan.FromMilliseconds(250),
        MaxSessionAge: TimeSpan.FromMilliseconds(2000),
        MaxLapAge: TimeSpan.FromMilliseconds(1000),
        MaxStatusAge: TimeSpan.FromMilliseconds(1000),
        MaxFrameLag: 2);
}
