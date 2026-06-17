using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App;

internal sealed record PhprWorkflowStatusSnapshot(
    PhprWorkflowDiagnosticsSnapshot? WorkflowDiagnostics,
    string AudioProfileName,
    string PhprProfileName,
    string CoexistenceStatus,
    bool EmergencyStopActive,
    bool ValidationBlocked,
    string BrakePulseText,
    string ThrottlePulseText,
    string GearPulseLatencyText,
    bool RealRoadVibrationEnabled,
    string RoadBrakeText,
    string RoadThrottleText,
    string LastRoadRoutingText,
    bool RealSlipLockEnabled,
    string SlipEffectText,
    string LockEffectText,
    string LastSlipLockRoutingText,
    string MockGearTarget,
    long SharedMockAcceptedCommandCount,
    long SharedMockPendingStopCount,
    long RealReportWriteCount,
    long RealFailedReportWriteCount,
    string RealConnectionState,
    string RealLastError,
    PhprLiveF1ValidationSnapshot? LiveValidation);

internal sealed record PhprWorkflowStatusBuildInputs(
    PhprWorkflowDiagnosticsSnapshot? WorkflowDiagnostics,
    string AudioProfileName,
    string PhprProfileName,
    string CoexistenceStatus,
    bool EmergencyStopActive,
    bool ValidationBlocked,
    string BrakePulseText,
    string ThrottlePulseText,
    string GearPulseLatencyText,
    bool RealRoadVibrationEnabled,
    string RoadBrakeText,
    string RoadThrottleText,
    string LastRoadRoutingText,
    bool RealSlipLockEnabled,
    string SlipEffectText,
    string LockEffectText,
    string LastSlipLockRoutingText,
    string MockGearTarget,
    long SharedMockAcceptedCommandCount,
    long SharedMockPendingStopCount,
    long RealReportWriteCount,
    long RealFailedReportWriteCount,
    string RealConnectionState,
    string RealLastError,
    PhprLiveF1ValidationSnapshot? LiveValidation);

internal sealed record PhprWorkflowStatusPresentation(
    string StatusText,
    IReadOnlyList<string> Items,
    string ValidationStatusText,
    IReadOnlyList<string> ValidationItems,
    string ProfilePersistenceDiagnosticsLine,
    string WorkflowDiagnosticsLine,
    string LiveValidationDiagnosticsLine);

internal static class PhprWorkflowStatusSnapshotBuilder
{
    public static PhprWorkflowStatusSnapshot Build(PhprWorkflowStatusBuildInputs? inputs)
    {
        return inputs is null
            ? new PhprWorkflowStatusSnapshot(
                WorkflowDiagnostics: null,
                AudioProfileName: string.Empty,
                PhprProfileName: string.Empty,
                CoexistenceStatus: string.Empty,
                EmergencyStopActive: false,
                ValidationBlocked: true,
                BrakePulseText: string.Empty,
                ThrottlePulseText: string.Empty,
                GearPulseLatencyText: string.Empty,
                RealRoadVibrationEnabled: false,
                RoadBrakeText: string.Empty,
                RoadThrottleText: string.Empty,
                LastRoadRoutingText: string.Empty,
                RealSlipLockEnabled: false,
                SlipEffectText: string.Empty,
                LockEffectText: string.Empty,
                LastSlipLockRoutingText: string.Empty,
                MockGearTarget: string.Empty,
                SharedMockAcceptedCommandCount: 0,
                SharedMockPendingStopCount: 0,
                RealReportWriteCount: 0,
                RealFailedReportWriteCount: 0,
                RealConnectionState: string.Empty,
                RealLastError: string.Empty,
                LiveValidation: null)
            : new PhprWorkflowStatusSnapshot(
                inputs.WorkflowDiagnostics,
                inputs.AudioProfileName,
                inputs.PhprProfileName,
                inputs.CoexistenceStatus,
                inputs.EmergencyStopActive,
                inputs.ValidationBlocked,
                inputs.BrakePulseText,
                inputs.ThrottlePulseText,
                inputs.GearPulseLatencyText,
                inputs.RealRoadVibrationEnabled,
                inputs.RoadBrakeText,
                inputs.RoadThrottleText,
                inputs.LastRoadRoutingText,
                inputs.RealSlipLockEnabled,
                inputs.SlipEffectText,
                inputs.LockEffectText,
                inputs.LastSlipLockRoutingText,
                inputs.MockGearTarget,
                inputs.SharedMockAcceptedCommandCount,
                inputs.SharedMockPendingStopCount,
                inputs.RealReportWriteCount,
                inputs.RealFailedReportWriteCount,
                inputs.RealConnectionState,
                inputs.RealLastError,
                inputs.LiveValidation);
    }
}

internal static class PhprWorkflowStatusPresenter
{
    public static PhprWorkflowStatusPresentation Build(PhprWorkflowStatusSnapshot? snapshot)
    {
        var workflow = snapshot?.WorkflowDiagnostics ?? CreateDefaultWorkflowDiagnostics();
        var liveValidation = snapshot?.LiveValidation ?? CreateDefaultLiveValidationSnapshot();
        var validation = PhprLiveF1ValidationGuide.Build(liveValidation);
        var selectedOutput = workflow.SelectedOutputIsConfigured
            ? "selected for this session"
            : "not selected";
        var warning = workflow.RealDirectControlEnabled
            ? "Real direct-control state is runtime-only; profile/app settings do not save enable, private device path, emergency stop, or write history."
            : "Real direct control is currently disabled; mock routing and diagnostics remain hardware-safe.";

        return new PhprWorkflowStatusPresentation(
            StatusText:
            $"P-HPR mode: {Normalize(workflow.Mode, "Disabled")}; telemetry input {Normalize(workflow.PipelineInputSource, "Unknown")}; replay source {Normalize(workflow.ReplaySource, "none")}; selected output {selectedOutput}; coexistence {Normalize(snapshot?.CoexistenceStatus, "Unknown")}; direct control {(workflow.RealDirectControlEnabled ? "enabled" : "disabled")}; emergency stop {snapshot?.EmergencyStopActive ?? false}; validation {((snapshot?.ValidationBlocked ?? true) ? "blocked" : "ready")}.",
            Items:
            [
                warning,
                $"Replay validation: input {Normalize(workflow.PipelineInputSource, "Unknown")}; replay source {Normalize(workflow.ReplaySource, "none")}; replay packets {workflow.ReplayPacketsReplayed:N0}; replay does not synthesize gear-paddle events.",
                $"Profiles: audio {Normalize(snapshot?.AudioProfileName, "default.hdprofile.json")} auto-saves current rig tuning/defaults; P-HPR {Normalize(snapshot?.PhprProfileName, "p-hpr.hdphprprofile.json")} is a manual effect-preferences snapshot only.",
                $"Instant gear pulse: brake {Normalize(snapshot?.BrakePulseText, "off 0%/0 Hz/0 ms")}; throttle {Normalize(snapshot?.ThrottlePulseText, "off 0%/0 Hz/0 ms")}; last latency {Normalize(snapshot?.GearPulseLatencyText, "none")}.",
                $"Road vibration: {FormatEnabled(snapshot?.RealRoadVibrationEnabled ?? false)}; brake {Normalize(snapshot?.RoadBrakeText, "off strength 0-0%; freq 0-0 Hz; duration 0 ms")}; throttle {Normalize(snapshot?.RoadThrottleText, "off strength 0-0%; freq 0-0 Hz; duration 0 ms")}; last {Normalize(snapshot?.LastRoadRoutingText, "none")}.",
                $"Slip/lock: {FormatEnabled(snapshot?.RealSlipLockEnabled ?? false)}; slip {Normalize(snapshot?.SlipEffectText, "off target Brake; strength 0-0%; freq 0-0 Hz; duration 0 ms")}; lock {Normalize(snapshot?.LockEffectText, "off target Brake; strength 0-0%; freq 0-0 Hz; duration 0 ms")}; last {Normalize(snapshot?.LastSlipLockRoutingText, "none")}.",
                $"Mock routing: gear {FormatEnabled(workflow.MockGearRoutingEnabled)} target {Normalize(snapshot?.MockGearTarget, "Brake")}; pedal effects {FormatEnabled(workflow.MockPedalEffectsEnabled)}; shared mock commands {snapshot?.SharedMockAcceptedCommandCount ?? 0:N0}; pending stops {snapshot?.SharedMockPendingStopCount ?? 0:N0}.",
                $"Real output counters: writes {snapshot?.RealReportWriteCount ?? 0:N0}; failures {snapshot?.RealFailedReportWriteCount ?? 0:N0}; connection {Normalize(snapshot?.RealConnectionState, "Unknown")}; last error {Normalize(snapshot?.RealLastError, "none")}."
            ],
            ValidationStatusText: validation.Summary,
            ValidationItems: validation.Checklist,
            ProfilePersistenceDiagnosticsLine: PhprWorkflowDiagnosticsReport.BuildProfilePersistenceLine(
                HapticProfileStore.GetDefaultProfilePath(),
                PhprEffectProfileStore.GetDefaultProfilePath()),
            WorkflowDiagnosticsLine: PhprWorkflowDiagnosticsReport.BuildWorkflowLine(workflow),
            LiveValidationDiagnosticsLine: validation.DiagnosticsLine);
    }

    private static PhprWorkflowDiagnosticsSnapshot CreateDefaultWorkflowDiagnostics()
    {
        return new PhprWorkflowDiagnosticsSnapshot(
            Mode: "Disabled",
            PipelineInputSource: "Unknown",
            ReplaySource: "none",
            ReplayPacketsReplayed: 0,
            RealDirectControlEnabled: false,
            RealDirectControlArmed: false,
            SelectedOutputIsConfigured: false,
            MockGearRoutingEnabled: false,
            MockPedalEffectsEnabled: false,
            RealRoadVibrationEnabled: false,
            RealSlipLockEnabled: false);
    }

    private static PhprLiveF1ValidationSnapshot CreateDefaultLiveValidationSnapshot()
    {
        return new PhprLiveF1ValidationSnapshot(
            TelemetryInputSource: "Unknown",
            PipelineRunning: false,
            UdpReceiverRunning: false,
            UdpPacketCount: 0,
            ParserSuccessCount: 0,
            TelemetryAge: null,
            TelemetryTimedOutMuted: true,
            DrivingArmed: false,
            DrivingArmedReason: "unknown",
            PaddleListenerStatus: "Unknown",
            ShiftIntentEnabled: false,
            AcceptedShiftIntentCount: 0,
            SuppressedShiftIntentCount: 0,
            OutputMode: "Disabled",
            MockGearRoutingEnabled: false,
            DirectControlEnabled: false,
            DirectControlArmed: false,
            SelectedOutputConfigured: false,
            CoexistenceStatus: "Unknown",
            EmergencyStopActive: false,
            RealRoadVibrationEnabled: false,
            RealSlipLockEnabled: false);
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string FormatEnabled(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }
}
