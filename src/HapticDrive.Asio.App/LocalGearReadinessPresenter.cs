namespace HapticDrive.Asio.App;

internal sealed record LocalGearReadinessPresentation(
    string StatusText,
    bool StartListenerEnabled,
    string StartListenerToolTip);

internal static class LocalGearReadinessPresenter
{
    public static LocalGearReadinessPresentation Build(
        LocalGearTestReadiness? readiness,
        bool autoStartListener)
    {
        var safeReadiness = readiness ?? new LocalGearTestReadiness(
            IsEnabled: false,
            IsReady: false,
            CanStartListener: false,
            Message: "Local gear test status unavailable.");

        return new LocalGearReadinessPresentation(
            StatusText: $"{safeReadiness.Message} Auto-start listener {autoStartListener}; Start Haptics required: NO; F1 telemetry required: NO; live telemetry effects started: NO.",
            StartListenerEnabled: safeReadiness.CanStartListener,
            StartListenerToolTip: safeReadiness.CanStartListener
                ? "Start the read-only paddle listener for Local Gear Test Mode without Start Haptics or F1 telemetry."
                : safeReadiness.Message);
    }
}
