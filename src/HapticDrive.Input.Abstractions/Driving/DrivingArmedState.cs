namespace HapticDrive.Input.Abstractions.Driving;

public sealed record DrivingArmedState(
    bool IsArmed,
    string Reason,
    DateTimeOffset UpdatedUtc,
    TimeSpan? TelemetryAge = null,
    bool MenuSafeModeEnabled = true,
    bool RequireRecentTelemetry = true)
{
    public static DrivingArmedState Default { get; } = NotArmed("No recent valid telemetry has been observed.");

    public static DrivingArmedState Armed(
        string reason,
        DateTimeOffset? updatedUtc = null,
        TimeSpan? telemetryAge = null,
        bool menuSafeModeEnabled = true,
        bool requireRecentTelemetry = true)
    {
        return new DrivingArmedState(
            true,
            SanitizeReason(reason, "Driving is armed."),
            updatedUtc ?? DateTimeOffset.UtcNow,
            telemetryAge,
            menuSafeModeEnabled,
            requireRecentTelemetry);
    }

    public static DrivingArmedState NotArmed(
        string reason,
        DateTimeOffset? updatedUtc = null,
        TimeSpan? telemetryAge = null,
        bool menuSafeModeEnabled = true,
        bool requireRecentTelemetry = true)
    {
        return new DrivingArmedState(
            false,
            SanitizeReason(reason, "Driving is not armed."),
            updatedUtc ?? DateTimeOffset.UtcNow,
            telemetryAge,
            menuSafeModeEnabled,
            requireRecentTelemetry);
    }

    private static string SanitizeReason(string reason, string fallback)
    {
        return string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
    }
}
