using HapticDrive.Asio.Core.Audio;
using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Asio.App.Tests;

public sealed class DashboardStatusPresenterTests
{
    [Fact]
    public void Build_MapsNoPacketsYetAndHapticsStoppedState()
    {
        var presentation = DashboardStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("Null Output", presentation.OutputModeValueText);
        Assert.Equal("Waiting", presentation.HeaderParserValueText);
        Assert.Equal("Waiting", presentation.VehicleStateValueText);
        Assert.Equal("Ready to capture F1 25 UDP packets to replay files.", presentation.RecordingDetailText);
        Assert.Contains("Start F1 25 UDP or replay a recording", presentation.NextStepText, StringComparison.Ordinal);
        Assert.Contains("Telemetry: Listening on port 20778; no packets yet.", presentation.ChecklistItems);
    }

    [Fact]
    public void Build_MapsAsioSelectedButStoppedAndNotArmedState()
    {
        var presentation = DashboardStatusPresenter.Build(CreateSnapshot(
            outputKind: AudioOutputDeviceKind.Asio,
            outputDisplayName: "ASIO Output",
            selectedAsioDriverName: "M-Audio",
            selectedOutputChannel: 1,
            asioArmed: false));

        Assert.Contains("ASIO is selected.", presentation.OutputModeDetailText, StringComparison.Ordinal);
        Assert.Contains("Arm ASIO on Devices", presentation.NextStepText, StringComparison.Ordinal);
        Assert.Contains("Output: ASIO selected with driver M-Audio and channel 1; arm it before starting output.", presentation.ChecklistItems);
    }

    [Fact]
    public void Build_MapsDirectPreferredButBlockedState()
    {
        var presentation = DashboardStatusPresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            telemetryPacketCount: 120,
            phprMode: DashboardPhprMode.Direct,
            phprDirectReady: false,
            phprDirectBlockedReason: "selected device is not ready"));

        Assert.Contains("P-HPR direct blocked", presentation.WorkflowStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-HPR Direct is selected but blocked.", presentation.NextStepText, StringComparison.Ordinal);
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Direct mode is selected but blocked: selected device is not ready.", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_MapsReplayActiveState()
    {
        var presentation = DashboardStatusPresenter.Build(CreateSnapshot(
            hapticsStarted: true,
            telemetryPacketCount: 120,
            replayActive: true,
            replayPacketCount: 42,
            replayFileName: "session.hdrec"));

        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Replay active with 42 packet(s).", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Replay is active from session.hdrec.", StringComparison.Ordinal));
        Assert.Contains("Replay is driving the app.", presentation.NextStepText, StringComparison.Ordinal);
    }

    private static DashboardStatusSnapshot CreateSnapshot(
        bool showWorkflowCard = true,
        string outputDisplayName = "Null Output",
        AudioOutputDeviceKind outputKind = AudioOutputDeviceKind.Null,
        bool outputHardwareArmed = false,
        int? selectedOutputChannel = null,
        string? selectedAsioDriverName = null,
        string hapticsStateText = "Stopped",
        bool hapticsStarted = false,
        bool asioArmed = false,
        bool telemetryUnavailable = false,
        string? telemetryError = null,
        bool telemetryRunning = true,
        bool telemetryHasNoPacketWarning = true,
        int telemetryBoundPort = 20778,
        TimeSpan? telemetryTimeSinceLastPacket = null,
        long telemetryPacketCount = 0,
        double telemetryPacketRatePerSecond = 0d,
        bool forwardingEnabled = false,
        int forwardingEnabledDestinationCount = 0,
        long forwardedDatagramCount = 0,
        long forwardedByteCount = 0,
        int forwardingDestinationCount = 0,
        long forwardingInputPacketCount = 0,
        long parserSuccessCount = 0,
        long parserIgnoredCount = 0,
        long parserFailureCount = 0,
        string lastPacketMessage = "No packets parsed yet.",
        long vehicleStateUpdateCount = 0,
        int? vehiclePlayerCarIndex = null,
        int? vehicleSpeedKph = null,
        int? vehicleGear = null,
        string lastVehicleStateMessage = "No vehicle samples mapped yet.",
        bool recordingHasError = false,
        string? recordingError = null,
        bool recordingActive = false,
        long recordingPacketCount = 0,
        string? recordingFileName = null,
        TimeSpan? recordingLastPacketRelativeTime = null,
        bool replayActive = false,
        long replayPacketCount = 0,
        string? replayFileName = null,
        DashboardPhprMode phprMode = DashboardPhprMode.Disabled,
        bool phprDirectReady = false,
        string? phprDirectBlockedReason = null,
        InputListenerStatus paddleListenerStatus = InputListenerStatus.Stopped,
        bool paddleMappingReady = false,
        bool bst1PaddleGearPulseEnabled = false,
        bool shiftIntentEnabled = false)
    {
        return new DashboardStatusSnapshot(
            ShowWorkflowCard: showWorkflowCard,
            OutputDisplayName: outputDisplayName,
            OutputKind: outputKind,
            OutputHardwareArmed: outputHardwareArmed,
            SelectedOutputChannel: selectedOutputChannel,
            SelectedAsioDriverName: selectedAsioDriverName,
            HapticsStateText: hapticsStateText,
            HapticsStarted: hapticsStarted,
            AsioArmed: asioArmed,
            TelemetryUnavailable: telemetryUnavailable,
            TelemetryError: telemetryError,
            TelemetryRunning: telemetryRunning,
            TelemetryHasNoPacketWarning: telemetryHasNoPacketWarning,
            TelemetryBoundPort: telemetryBoundPort,
            TelemetryTimeSinceLastPacket: telemetryTimeSinceLastPacket,
            TelemetryPacketCount: telemetryPacketCount,
            TelemetryPacketRatePerSecond: telemetryPacketRatePerSecond,
            ForwardingEnabled: forwardingEnabled,
            ForwardingEnabledDestinationCount: forwardingEnabledDestinationCount,
            ForwardedDatagramCount: forwardedDatagramCount,
            ForwardedByteCount: forwardedByteCount,
            ForwardingDestinationCount: forwardingDestinationCount,
            ForwardingInputPacketCount: forwardingInputPacketCount,
            ParserSuccessCount: parserSuccessCount,
            ParserIgnoredCount: parserIgnoredCount,
            ParserFailureCount: parserFailureCount,
            LastPacketMessage: lastPacketMessage,
            VehicleStateUpdateCount: vehicleStateUpdateCount,
            VehiclePlayerCarIndex: vehiclePlayerCarIndex,
            VehicleSpeedKph: vehicleSpeedKph,
            VehicleGear: vehicleGear,
            LastVehicleStateMessage: lastVehicleStateMessage,
            RecordingHasError: recordingHasError,
            RecordingError: recordingError,
            RecordingActive: recordingActive,
            RecordingPacketCount: recordingPacketCount,
            RecordingFileName: recordingFileName,
            RecordingLastPacketRelativeTime: recordingLastPacketRelativeTime,
            ReplayActive: replayActive,
            ReplayPacketCount: replayPacketCount,
            ReplayFileName: replayFileName,
            PhprMode: phprMode,
            PhprDirectReady: phprDirectReady,
            PhprDirectBlockedReason: phprDirectBlockedReason,
            PaddleListenerStatus: paddleListenerStatus,
            PaddleMappingReady: paddleMappingReady,
            Bst1PaddleGearPulseEnabled: bst1PaddleGearPulseEnabled,
            ShiftIntentEnabled: shiftIntentEnabled);
    }
}
