namespace HapticDrive.Asio.App;

internal sealed record PhprWorkflowDiagnosticsSnapshot(
    string Mode,
    string PipelineInputSource,
    string ReplaySource,
    long ReplayPacketsReplayed,
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
        return $"Profiles: audio {audioProfilePath}; P-HPR {phprProfilePath}; P-HPR profile contains effect preferences only and excludes direct-enable/device/emergency state.";
    }

    public static string BuildWorkflowLine(PhprWorkflowDiagnosticsSnapshot snapshot)
    {
        return $"P-HPR workflow: mode {snapshot.Mode}; telemetry input {snapshot.PipelineInputSource}; replay source {snapshot.ReplaySource}; replay packets {snapshot.ReplayPacketsReplayed:N0}; real direct {FormatEnabled(snapshot.RealDirectControlEnabled)}; selected output {snapshot.SelectedOutputIsConfigured}; mock gear {FormatEnabled(snapshot.MockGearRoutingEnabled)}; mock pedal effects {FormatEnabled(snapshot.MockPedalEffectsEnabled)}; road {FormatEnabled(snapshot.RealRoadVibrationEnabled)}; slip/lock {FormatEnabled(snapshot.RealSlipLockEnabled)}.";
    }

    private static string FormatEnabled(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }

}
