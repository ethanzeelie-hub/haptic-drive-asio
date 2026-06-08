namespace HapticDrive.Asio.App;

internal sealed record PhprLiveF1ValidationSnapshot(
    string TelemetryInputSource,
    bool PipelineRunning,
    bool UdpReceiverRunning,
    long UdpPacketCount,
    long ParserSuccessCount,
    TimeSpan? TelemetryAge,
    bool TelemetryTimedOutMuted,
    bool DrivingArmed,
    string DrivingArmedReason,
    string PaddleListenerStatus,
    bool ShiftIntentEnabled,
    long AcceptedShiftIntentCount,
    long SuppressedShiftIntentCount,
    string OutputMode,
    bool MockGearRoutingEnabled,
    bool DirectControlEnabled,
    bool DirectControlArmed,
    bool SelectedOutputConfigured,
    string CoexistenceStatus,
    bool EmergencyStopActive,
    bool RealRoadVibrationEnabled,
    bool RealSlipLockEnabled);

internal sealed record PhprLiveF1ValidationStatus(
    string Summary,
    IReadOnlyList<string> Checklist,
    string DiagnosticsLine);

internal static class PhprLiveF1ValidationGuide
{
    public static PhprLiveF1ValidationStatus Build(PhprLiveF1ValidationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var liveTelemetryActive = IsLiveTelemetryActive(snapshot);
        var telemetryStatus = liveTelemetryActive
            ? $"active from live UDP; packets {snapshot.UdpPacketCount:N0}; parsed {snapshot.ParserSuccessCount:N0}; age {FormatAge(snapshot.TelemetryAge)}"
            : $"not yet active for live validation; source {snapshot.TelemetryInputSource}; receiver {(snapshot.UdpReceiverRunning ? "running" : "stopped")}; parsed {snapshot.ParserSuccessCount:N0}; stale mute {snapshot.TelemetryTimedOutMuted}";
        var directState = $"{(snapshot.DirectControlEnabled ? "enabled" : "disabled")}/{(snapshot.DirectControlArmed ? "armed" : "unarmed")}";
        var selectedOutput = snapshot.SelectedOutputConfigured ? "selected for this session" : "not selected";
        var emergency = snapshot.EmergencyStopActive ? "latched" : "clear";
        var driving = snapshot.DrivingArmed ? "true" : "false";
        var validationState = liveTelemetryActive
            && snapshot.DrivingArmed
            && snapshot.ShiftIntentEnabled
            && snapshot.MockGearRoutingEnabled
            && string.Equals(snapshot.CoexistenceStatus, "Clear", StringComparison.Ordinal)
            && !snapshot.EmergencyStopActive
                ? "ready for supervised live mock validation"
                : "pending live validation gates";

        var checklist = new[]
        {
            $"1. App open, direct control disabled: current direct control {directState}; selected output {selectedOutput}.",
            $"2. F1 25 telemetry active: {telemetryStatus}.",
            $"3. DrivingArmed true in session: current DrivingArmed {driving}; reason {snapshot.DrivingArmedReason}.",
            $"4. Paddle press accepted: listener {snapshot.PaddleListenerStatus}; shift intent {(snapshot.ShiftIntentEnabled ? "enabled" : "disabled")}; accepted {snapshot.AcceptedShiftIntentCount:N0}; suppressed {snapshot.SuppressedShiftIntentCount:N0}.",
            $"5. Mock mode gear pulse diagnostics: output mode {snapshot.OutputMode}; mock gear routing {(snapshot.MockGearRoutingEnabled ? "enabled" : "disabled")}.",
            $"6. Real mode armed manually: direct control {directState}; selected output {selectedOutput}; only proceed under local supervision.",
            $"7. Brake/throttle gear pulse test: brake and throttle tests stay manual; automated verification uses fake output only.",
            $"8. Road vibration test: real road vibration {(snapshot.RealRoadVibrationEnabled ? "enabled" : "disabled")}; verify only after gear pulse and stop behavior are safe.",
            $"9. Slip/lock test if safe: real slip/lock {(snapshot.RealSlipLockEnabled ? "enabled" : "disabled")}; skip if track conditions or pedal behavior are unclear.",
            $"10. Menu/tabbing suppression: confirm DrivingArmed becomes false or paddles are suppressed when paused, tabbed, in menus, garage, or results.",
            $"11. Emergency stop: current emergency stop {emergency}; press it during manual validation and confirm both modules stop before clearing.",
            $"12. SimPro/SimHub conflict warning: coexistence {snapshot.CoexistenceStatus}; real direct starts require Clear."
        };

        var diagnosticsLine =
            $"P-HPR live F1 validation: {validationState}; telemetry {telemetryStatus}; DrivingArmed {driving}; paddle listener {snapshot.PaddleListenerStatus}; shift intent {(snapshot.ShiftIntentEnabled ? "enabled" : "disabled")} accepted {snapshot.AcceptedShiftIntentCount:N0}/suppressed {snapshot.SuppressedShiftIntentCount:N0}; output mode {snapshot.OutputMode}; direct {directState}; selected output {snapshot.SelectedOutputConfigured}; coexistence {snapshot.CoexistenceStatus}; emergency stop {emergency}; physical validation pending local Ethan run.";

        return new PhprLiveF1ValidationStatus(
            $"Live F1 P-HPR validation: {validationState}; telemetry {telemetryStatus}; DrivingArmed {driving}; output mode {snapshot.OutputMode}; emergency stop {emergency}.",
            checklist,
            diagnosticsLine);
    }

    private static bool IsLiveTelemetryActive(PhprLiveF1ValidationSnapshot snapshot)
    {
        return snapshot.PipelineRunning
            && string.Equals(snapshot.TelemetryInputSource, "LiveUdp", StringComparison.Ordinal)
            && snapshot.UdpReceiverRunning
            && snapshot.UdpPacketCount > 0
            && snapshot.ParserSuccessCount > 0
            && !snapshot.TelemetryTimedOutMuted;
    }

    private static string FormatAge(TimeSpan? age)
    {
        return age is null ? "unknown" : $"{age.Value.TotalMilliseconds:0} ms";
    }
}
