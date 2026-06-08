namespace HapticDrive.Asio.App;

internal sealed record PhprWorkflowDiagnosticsSnapshot(
    string Mode,
    bool RealDirectControlEnabled,
    bool RealDirectControlArmed,
    bool SelectedOutputIsConfigured,
    bool MockGearRoutingEnabled,
    bool MockPedalEffectsEnabled,
    bool RealRoadVibrationEnabled,
    bool RealSlipLockEnabled);

internal static class PhprWorkflowDiagnosticsReport
{
    public static string BuildProfilePersistenceLine(string audioProfilePath, string phprProfilePath)
    {
        return $"Profiles: audio {audioProfilePath}; P-HPR {phprProfilePath}; P-HPR profile contains effect preferences only and excludes arm/device/emergency state.";
    }

    public static string BuildWorkflowLine(PhprWorkflowDiagnosticsSnapshot snapshot)
    {
        return $"P-HPR workflow: mode {snapshot.Mode}; real direct {FormatEnabled(snapshot.RealDirectControlEnabled)}/{FormatArmed(snapshot.RealDirectControlArmed)}; selected output {snapshot.SelectedOutputIsConfigured}; mock gear {FormatEnabled(snapshot.MockGearRoutingEnabled)}; mock pedal effects {FormatEnabled(snapshot.MockPedalEffectsEnabled)}; road {FormatEnabled(snapshot.RealRoadVibrationEnabled)}; slip/lock {FormatEnabled(snapshot.RealSlipLockEnabled)}.";
    }

    private static string FormatEnabled(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }

    private static string FormatArmed(bool armed)
    {
        return armed ? "armed" : "unarmed";
    }
}
