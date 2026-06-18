using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Asio.App;

internal enum DashboardPhprMode
{
    Disabled = 0,
    Mock = 1,
    Direct = 2
}

internal sealed record DashboardStatusSnapshot(
    bool ShowWorkflowCard,
    string OutputDisplayName,
    AudioOutputDeviceKind OutputKind,
    bool OutputHardwareArmed,
    int? SelectedOutputChannel,
    string? SelectedAsioDriverName,
    string HapticsStateText,
    bool HapticsStarted,
    bool AsioArmed,
    bool TelemetryUnavailable,
    string? TelemetryError,
    bool TelemetryRunning,
    bool TelemetryHasNoPacketWarning,
    int TelemetryBoundPort,
    TimeSpan? TelemetryTimeSinceLastPacket,
    long TelemetryPacketCount,
    double TelemetryPacketRatePerSecond,
    bool ForwardingEnabled,
    int ForwardingEnabledDestinationCount,
    long ForwardedDatagramCount,
    long ForwardedByteCount,
    int ForwardingDestinationCount,
    long ForwardingInputPacketCount,
    long ParserSuccessCount,
    long ParserIgnoredCount,
    long ParserFailureCount,
    string LastPacketMessage,
    long VehicleStateUpdateCount,
    int? VehiclePlayerCarIndex,
    int? VehicleSpeedKph,
    int? VehicleGear,
    string LastVehicleStateMessage,
    bool RecordingHasError,
    string? RecordingError,
    bool RecordingActive,
    long RecordingPacketCount,
    string? RecordingFileName,
    TimeSpan? RecordingLastPacketRelativeTime,
    bool ReplayActive,
    long ReplayPacketCount,
    string? ReplayFileName,
    DashboardPhprMode PhprMode,
    bool PhprDirectReady,
    string? PhprDirectBlockedReason,
    InputListenerStatus PaddleListenerStatus,
    bool PaddleMappingReady,
    bool Bst1PaddleGearPulseEnabled,
    bool ShiftIntentEnabled,
    int? RecordingQueueCapacityPackets = null,
    int RecordingQueuedPacketCount = 0,
    long RecordingDroppedPacketCount = 0);

internal sealed record DashboardStatusPresentation(
    bool ShowWorkflowCard,
    string OutputModeValueText,
    string OutputModeDetailText,
    string HapticsStateText,
    string UdpListenerValueText,
    string UdpListenerDetailText,
    string PacketCountValueText,
    string PacketRateDetailText,
    string ForwardingValueText,
    string ForwardingDetailText,
    string HeaderParserValueText,
    string HeaderParserDetailText,
    string VehicleStateValueText,
    string VehicleStateDetailText,
    string RecordingValueText,
    string RecordingDetailText,
    string WorkflowStatusText,
    string NextStepText,
    IReadOnlyList<string> ChecklistItems,
    string PageStatusText);

internal static class DashboardStatusPresenter
{
    public static DashboardStatusPresentation Build(DashboardStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var outputDetail = BuildOutputModeDetailText(snapshot);
        var telemetryValue = snapshot.TelemetryUnavailable
            ? "Unavailable"
            : snapshot.TelemetryRunning
                ? $"Listening {snapshot.TelemetryBoundPort}"
                : "Stopped";
        var telemetryDetail = snapshot.TelemetryUnavailable
            ? snapshot.TelemetryError ?? "UDP listener unavailable."
            : !snapshot.TelemetryRunning
                ? "Listener stopped."
                : snapshot.TelemetryPacketCount == 0
                    ? $"Listening on port {snapshot.TelemetryBoundPort}; no packets received yet."
                    : snapshot.TelemetryTimeSinceLastPacket is null
                        ? "Packets received; last-packet timing unavailable."
                        : $"Last packet {snapshot.TelemetryTimeSinceLastPacket.Value.TotalSeconds:0.0}s ago.";
        var parserIdle = snapshot.ParserSuccessCount == 0
            && snapshot.ParserIgnoredCount == 0
            && snapshot.ParserFailureCount == 0;
        var parserValue = parserIdle
            ? "Waiting"
            : $"{snapshot.ParserSuccessCount:N0} valid";
        var parserDetail = parserIdle
            ? "Waiting for F1 25 packets."
            : $"Ignored {snapshot.ParserIgnoredCount:N0}, failed {snapshot.ParserFailureCount:N0}. {snapshot.LastPacketMessage}";
        var vehicleStateValue = snapshot.VehicleStateUpdateCount == 0
            ? "Waiting"
            : $"{snapshot.VehicleStateUpdateCount:N0} updates";
        var vehicleStateDetail = snapshot.VehicleSpeedKph is not null && snapshot.VehicleGear is not null && snapshot.VehiclePlayerCarIndex is not null
            ? $"Player {snapshot.VehiclePlayerCarIndex}, {snapshot.VehicleSpeedKph} km/h, gear {snapshot.VehicleGear}."
            : snapshot.VehicleStateUpdateCount == 0
                ? "Waiting for parsed telemetry samples."
                : snapshot.LastVehicleStateMessage;
        var recordingValue = snapshot.RecordingHasError
            ? "Error"
            : snapshot.RecordingActive
                ? $"{snapshot.RecordingPacketCount:N0} packets"
                : "Idle";
        var recordingDetail = BuildRecordingDetailText(snapshot);
        var outputSummary = BuildDashboardOutputSummary(snapshot);
        var telemetrySummary = BuildDashboardTelemetrySummary(snapshot);
        var phprSummary = BuildDashboardPhprSummary(snapshot);
        var paddleSummary = BuildDashboardPaddleSummary(snapshot);
        var recordingSummary = BuildDashboardRecordingSummary(snapshot);
        var nextStep = BuildNextStepText(snapshot);
        var workflowStatus =
            $"Haptics {(snapshot.HapticsStarted ? "running" : "stopped")}; output {snapshot.OutputDisplayName}; {BuildTelemetryStateText(snapshot).ToLowerInvariant()}; replay {(snapshot.ReplayActive ? "active" : "idle")}; {BuildPhprStateText(snapshot).ToLowerInvariant()}.";

        return new DashboardStatusPresentation(
            ShowWorkflowCard: snapshot.ShowWorkflowCard,
            OutputModeValueText: snapshot.OutputDisplayName,
            OutputModeDetailText: outputDetail,
            HapticsStateText: snapshot.HapticsStateText,
            UdpListenerValueText: telemetryValue,
            UdpListenerDetailText: telemetryDetail,
            PacketCountValueText: snapshot.TelemetryPacketCount.ToString("N0"),
            PacketRateDetailText: $"{snapshot.TelemetryPacketRatePerSecond:0.00} packets/s",
            ForwardingValueText: snapshot.ForwardingEnabled
                ? $"{snapshot.ForwardingEnabledDestinationCount} enabled"
                : "Disabled",
            ForwardingDetailText: snapshot.ForwardingEnabled
                ? $"{snapshot.ForwardedDatagramCount:N0} datagrams, {snapshot.ForwardedByteCount:N0} bytes."
                : $"{snapshot.ForwardingDestinationCount} destinations configured; {snapshot.ForwardingInputPacketCount:N0} packets observed.",
            HeaderParserValueText: parserValue,
            HeaderParserDetailText: parserDetail,
            VehicleStateValueText: vehicleStateValue,
            VehicleStateDetailText: vehicleStateDetail,
            RecordingValueText: recordingValue,
            RecordingDetailText: recordingDetail,
            WorkflowStatusText: workflowStatus,
            NextStepText: nextStep,
            ChecklistItems:
            [
                $"Output: {outputSummary}",
                $"Telemetry: {telemetrySummary}",
                $"P-HPR pedals: {phprSummary}",
                $"Wheel paddles: {paddleSummary}",
                $"Recording / replay: {recordingSummary}"
            ],
            PageStatusText: $"Haptics {(snapshot.HapticsStarted ? "running" : "stopped")}; {outputSummary}; {telemetrySummary.ToLowerInvariant()}.");
    }

    private static string BuildOutputModeDetailText(DashboardStatusSnapshot snapshot)
    {
        if (snapshot.OutputKind != AudioOutputDeviceKind.Asio)
        {
            return "Null output remains the safe default until hardware is explicitly selected.";
        }

        return snapshot.HapticsStarted
            ? $"ASIO active on driver {Normalize(snapshot.SelectedAsioDriverName, "none")}; channel {(snapshot.SelectedOutputChannel is null ? "not selected" : snapshot.SelectedOutputChannel)}."
            : snapshot.OutputHardwareArmed
                ? $"ASIO is selected and armed. Driver {Normalize(snapshot.SelectedAsioDriverName, "none")}; channel {(snapshot.SelectedOutputChannel is null ? "not selected" : snapshot.SelectedOutputChannel)}; press Start Haptics to open the stream."
                : $"ASIO is selected. Driver {Normalize(snapshot.SelectedAsioDriverName, "none")}; channel {(snapshot.SelectedOutputChannel is null ? "not selected" : snapshot.SelectedOutputChannel)}; arm it before starting haptics.";
    }

    private static string BuildRecordingDetailText(DashboardStatusSnapshot snapshot)
    {
        if (snapshot.RecordingHasError)
        {
            return snapshot.RecordingError ?? "Recording error.";
        }

        if (snapshot.RecordingActive)
        {
            var detail = snapshot.RecordingLastPacketRelativeTime is null
                ? $"Writing {Normalize(snapshot.RecordingFileName, "recording")}; waiting for first packet."
                : $"Writing {Normalize(snapshot.RecordingFileName, "recording")}; last packet {snapshot.RecordingLastPacketRelativeTime.Value.TotalSeconds:0.000}s.";

            if (snapshot.RecordingDroppedPacketCount > 0)
            {
                detail += $" Queue {snapshot.RecordingQueuedPacketCount:N0}/{snapshot.RecordingQueueCapacityPackets ?? 0:N0}; dropped {snapshot.RecordingDroppedPacketCount:N0}.";
            }

            return detail;
        }

        return "Ready to capture F1 25 UDP packets to replay files.";
    }

    private static string BuildTelemetryStateText(DashboardStatusSnapshot snapshot)
    {
        return snapshot.TelemetryUnavailable
            ? "UDP unavailable"
            : snapshot.TelemetryHasNoPacketWarning
                ? "Waiting for UDP"
                : snapshot.TelemetryRunning
                    ? $"Listening on {snapshot.TelemetryBoundPort}"
                    : "UDP stopped";
    }

    private static string BuildPhprStateText(DashboardStatusSnapshot snapshot)
    {
        return snapshot.PhprMode switch
        {
            DashboardPhprMode.Disabled => "P-HPR disabled",
            DashboardPhprMode.Mock => "P-HPR mock ready",
            DashboardPhprMode.Direct when snapshot.PhprDirectReady => "P-HPR direct ready",
            DashboardPhprMode.Direct => $"P-HPR direct blocked: {NormalizeDashboardReason(snapshot.PhprDirectBlockedReason)}",
            _ => "P-HPR unavailable"
        };
    }

    private static string BuildNextStepText(DashboardStatusSnapshot snapshot)
    {
        if (snapshot.TelemetryUnavailable)
        {
            return "Fix the UDP listener startup error before using live telemetry or recordings.";
        }

        if (!snapshot.HapticsStarted)
        {
            if (snapshot.OutputKind == AudioOutputDeviceKind.Asio && !snapshot.AsioArmed)
            {
                return "Arm ASIO on Devices, then press Start Haptics when you are ready to open the bass-shaker stream.";
            }

            if (snapshot.OutputKind == AudioOutputDeviceKind.Asio)
            {
                return "Press Start Haptics when you are ready to open the selected ASIO driver and channel.";
            }

            if (snapshot.TelemetryPacketCount == 0 && !snapshot.ReplayActive)
            {
                return "Start F1 25 UDP or replay a recording to feed the app, then press Start Haptics when you want live output.";
            }

            return "Press Start Haptics when you are ready to run the current setup.";
        }

        if (snapshot.TelemetryPacketCount == 0 && !snapshot.ReplayActive)
        {
            return "Haptics are running, but no packets are arriving yet. Start F1 25 UDP or replay a saved recording.";
        }

        if (snapshot.PhprMode == DashboardPhprMode.Direct && !snapshot.PhprDirectReady)
        {
            return $"P-HPR Direct is selected but blocked. Use Devices to clear readiness issues or switch to Mock. ({NormalizeDashboardReason(snapshot.PhprDirectBlockedReason)})";
        }

        if (snapshot.PaddleListenerStatus is not InputListenerStatus.Listening
            && (snapshot.Bst1PaddleGearPulseEnabled || snapshot.ShiftIntentEnabled))
        {
            return "Start the wheel paddle listener on Devices if you want local paddle gear pulses or live shift intent routing.";
        }

        return snapshot.ReplayActive
            ? "Replay is driving the app. Let it run or stop replay when you are ready to return to live UDP."
            : "The app is ready. Drive the car, keep telemetry flowing, and use Devices or Testing / Validation only when you need setup or checks.";
    }

    private static string BuildDashboardOutputSummary(DashboardStatusSnapshot snapshot)
    {
        if (snapshot.OutputKind != AudioOutputDeviceKind.Asio)
        {
            return $"{snapshot.OutputDisplayName} selected; safe default mode.";
        }

        if (!snapshot.AsioArmed)
        {
            return $"ASIO selected with driver {Normalize(snapshot.SelectedAsioDriverName, "none")} and channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}; arm it before starting output.";
        }

        return snapshot.HapticsStarted
            ? $"ASIO active on driver {Normalize(snapshot.SelectedAsioDriverName, "none")} channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}."
            : $"ASIO is armed on driver {Normalize(snapshot.SelectedAsioDriverName, "none")} channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}; stream still stopped.";
    }

    private static string BuildDashboardTelemetrySummary(DashboardStatusSnapshot snapshot)
    {
        if (snapshot.TelemetryUnavailable)
        {
            return "Listener unavailable.";
        }

        if (snapshot.ReplayActive)
        {
            return $"Replay active with {snapshot.ReplayPacketCount:N0} packet(s).";
        }

        if (!snapshot.TelemetryRunning)
        {
            return "Listener stopped.";
        }

        return snapshot.TelemetryPacketCount == 0
            ? $"Listening on port {snapshot.TelemetryBoundPort}; no packets yet."
            : snapshot.TelemetryTimeSinceLastPacket is null
                ? $"Listening on port {snapshot.TelemetryBoundPort}; packets received."
                : $"Listening on port {snapshot.TelemetryBoundPort}; last packet {snapshot.TelemetryTimeSinceLastPacket.Value.TotalSeconds:0.0}s ago.";
    }

    private static string BuildDashboardPhprSummary(DashboardStatusSnapshot snapshot)
    {
        return snapshot.PhprMode switch
        {
            DashboardPhprMode.Disabled => "Disabled until you enable a pedal mode on Devices.",
            DashboardPhprMode.Mock => "Mock mode is selected for software-only pedal testing.",
            DashboardPhprMode.Direct when snapshot.PhprDirectReady => "Direct mode is selected and ready for this session.",
            DashboardPhprMode.Direct => $"Direct mode is selected but blocked: {NormalizeDashboardReason(snapshot.PhprDirectBlockedReason)}.",
            _ => "Unavailable."
        };
    }

    private static string BuildDashboardPaddleSummary(DashboardStatusSnapshot snapshot)
    {
        return snapshot.PaddleListenerStatus switch
        {
            InputListenerStatus.Listening when snapshot.PaddleMappingReady => "Listener running and left/right paddle mappings are ready.",
            InputListenerStatus.Listening => "Listener running, but left/right paddle mapping still needs to be completed.",
            _ when snapshot.PaddleMappingReady => "Mappings are saved; start the listener when you want paddle input.",
            _ => "Refresh devices, choose the wheel input, and map the left/right paddle buttons when needed."
        };
    }

    private static string BuildDashboardRecordingSummary(DashboardStatusSnapshot snapshot)
    {
        if (snapshot.RecordingActive)
        {
            return snapshot.RecordingDroppedPacketCount > 0
                ? $"Recording is active to {Normalize(snapshot.RecordingFileName, "recording")}; queue {snapshot.RecordingQueuedPacketCount:N0}/{snapshot.RecordingQueueCapacityPackets ?? 0:N0}; dropped {snapshot.RecordingDroppedPacketCount:N0}."
                : $"Recording is active to {Normalize(snapshot.RecordingFileName, "recording")}.";
        }

        return snapshot.ReplayActive
            ? $"Replay is active from {Normalize(snapshot.ReplayFileName, "replay")}."
            : "Recording idle; replay idle.";
    }

    private static string NormalizeDashboardReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "a readiness check is still blocking direct output"
            : reason;
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
    }
}
