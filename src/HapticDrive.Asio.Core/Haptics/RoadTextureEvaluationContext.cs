namespace HapticDrive.Asio.Core.Haptics;

public sealed record RoadTextureEvaluationContext(
    DateTimeOffset NowUtc,
    bool HapticsRunning,
    bool DrivingArmed,
    bool AllowWhenDrivingNotArmed,
    bool TelemetryStale,
    DateTimeOffset? LastGearPulseAtUtc)
{
    public static RoadTextureEvaluationContext Default { get; } = new(
        DateTimeOffset.UtcNow,
        HapticsRunning: true,
        DrivingArmed: true,
        AllowWhenDrivingNotArmed: true,
        TelemetryStale: false,
        LastGearPulseAtUtc: null);
}
