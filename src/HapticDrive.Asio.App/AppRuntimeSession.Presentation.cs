using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle.Freshness;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Telemetry;
using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Input.Windows;
using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Readiness;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Validation;
using HapticDrive.Simagic.PHPR.Output.Windows;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HapticDrive.Asio.App;

internal sealed partial class AppRuntimeSession
{
    private void UpdateHapticsControlState(HapticPipelineSnapshot? pipelineSnapshot = null)
    {
        pipelineSnapshot ??= RefreshDrivingArmedAndShiftIntentTelemetry();
        var presentation = HapticsControlStatePresenter.Build(new HapticsControlStateSnapshot(
            HapticsStarted: _hapticsStarted,
            PipelineRunning: pipelineSnapshot.IsRunning,
            EmergencyMuteActive: _emergencyMuted,
            NormalMuteActive: pipelineSnapshot.IsMuted,
            TelemetryTimedOutMuted: pipelineSnapshot.TelemetryTimedOutMuted,
            ActiveEffectCount: pipelineSnapshot.Effects.ActiveEffectCount,
            OutputPeakLevel: pipelineSnapshot.Audio?.OutputPeakLevel,
            OutputStatus: pipelineSnapshot.Output));
        StartStopButton.Content = presentation.StartStopButtonText;
        EmergencyMuteButton.Content = presentation.EmergencyMuteButtonText;
        ResetOutputInterlockButton.IsEnabled = pipelineSnapshot.OutputInterlock.IsLatched;
        UpdateDashboardStatus(pipelineSnapshot);
    }

    private void UpdateDashboardStatus(HapticPipelineSnapshot? pipelineSnapshot = null)
    {
        pipelineSnapshot ??= RefreshDrivingArmedAndShiftIntentTelemetry();
        var presentation = BuildDashboardStatusPresentation(pipelineSnapshot);
        DashboardViewControl.Apply(presentation);

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Dashboard" })
        {
            PageStatusText.Text = presentation.PageStatusText;
        }
    }

    private DashboardStatusPresentation BuildDashboardStatusPresentation(HapticPipelineSnapshot pipelineSnapshot)
    {
        var telemetrySnapshot = _telemetryReceiver.GetSnapshot();
        var replaySnapshot = pipelineSnapshot.Replay;
        var recordingSnapshot = pipelineSnapshot.Recording;
        var directRuntime = _phprDirectRuntime.GetSnapshot();
        var paddleSnapshot = _paddleInputSource.GetPaddleSnapshot();
        var shiftIntentDiagnostics = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var hapticsStatePresentation = HapticsControlStatePresenter.Build(new HapticsControlStateSnapshot(
            HapticsStarted: _hapticsStarted,
            PipelineRunning: pipelineSnapshot.IsRunning,
            EmergencyMuteActive: _emergencyMuted,
            NormalMuteActive: pipelineSnapshot.IsMuted,
            TelemetryTimedOutMuted: pipelineSnapshot.TelemetryTimedOutMuted,
            ActiveEffectCount: pipelineSnapshot.Effects.ActiveEffectCount,
            OutputPeakLevel: pipelineSnapshot.Audio?.OutputPeakLevel,
            OutputStatus: pipelineSnapshot.Output));

        return DashboardStatusPresenter.Build(new DashboardStatusSnapshot(
            ShowWorkflowCard: NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Dashboard" },
            OutputDisplayName: pipelineSnapshot.Output.DisplayName,
            OutputKind: pipelineSnapshot.Output.Kind,
            OutputHardwareArmed: pipelineSnapshot.Output.IsHardwareArmed,
            SelectedOutputChannel: pipelineSnapshot.Output.SelectedOutputChannel,
            SelectedAsioDriverName: _selectedAsioDriverName,
            HapticsStateText: hapticsStatePresentation.HapticsStateText,
            HapticsStarted: _hapticsStarted,
            AsioArmed: _asioArmed,
            TelemetryUnavailable: _telemetryStartError is not null,
            TelemetryError: _telemetryStartError,
            TelemetryRunning: telemetrySnapshot.IsRunning,
            TelemetryHasNoPacketWarning: telemetrySnapshot.HasNoPacketWarning,
            TelemetryBoundPort: telemetrySnapshot.BoundPort,
            TelemetryTimeSinceLastPacket: telemetrySnapshot.TimeSinceLastPacket,
            TelemetryPacketCount: telemetrySnapshot.PacketCount,
            TelemetryPacketRatePerSecond: telemetrySnapshot.PacketRatePerSecond,
            ForwardingEnabled: pipelineSnapshot.Forwarding.IsEnabled,
            ForwardingEnabledDestinationCount: pipelineSnapshot.Forwarding.EnabledDestinationCount,
            ForwardedDatagramCount: pipelineSnapshot.Forwarding.ForwardedDatagramCount,
            ForwardedByteCount: pipelineSnapshot.Forwarding.ForwardedByteCount,
            ForwardingDestinationCount: pipelineSnapshot.Forwarding.DestinationCount,
            ForwardingInputPacketCount: pipelineSnapshot.Forwarding.InputPacketCount,
            ParserSuccessCount: pipelineSnapshot.ParserSuccessCount,
            ParserIgnoredCount: pipelineSnapshot.ParserIgnoredCount,
            ParserFailureCount: pipelineSnapshot.ParserFailureCount,
            LastPacketMessage: pipelineSnapshot.LastPacketMessage,
            VehicleStateUpdateCount: pipelineSnapshot.VehicleStateUpdateCount,
            VehiclePlayerCarIndex: pipelineSnapshot.VehicleState.Telemetry is null ? null : pipelineSnapshot.VehicleState.Frame.PlayerCarIndex,
            VehicleSpeedKph: pipelineSnapshot.VehicleState.Telemetry?.Value.SpeedKph,
            VehicleGear: pipelineSnapshot.VehicleState.Telemetry?.Value.Gear,
            LastVehicleStateMessage: pipelineSnapshot.LastVehicleStateMessage,
            RecordingHasError: (_recordingError ?? recordingSnapshot.LastErrorMessage) is not null,
            RecordingError: _recordingError ?? recordingSnapshot.LastErrorMessage,
            RecordingActive: recordingSnapshot.IsRecording,
            RecordingPacketCount: recordingSnapshot.PacketCount,
            RecordingFileName: recordingSnapshot.FilePath is null ? null : Path.GetFileName(recordingSnapshot.FilePath),
            RecordingLastPacketRelativeTime: recordingSnapshot.LastPacketRelativeTime,
            RecordingQueueCapacityPackets: recordingSnapshot.QueueCapacityPackets,
            RecordingQueuedPacketCount: recordingSnapshot.QueuedPacketCount,
            RecordingDroppedPacketCount: recordingSnapshot.DroppedPacketCount,
            ReplayActive: replaySnapshot.IsReplaying,
            ReplayPacketCount: replaySnapshot.PacketsReplayed,
            ReplayFileName: replaySnapshot.SourceFilePath is null ? null : Path.GetFileName(replaySnapshot.SourceFilePath),
            PhprMode: ToDashboardPhprMode(GetSelectedPhprPedalsMode()),
            PhprDirectReady: directRuntime.DirectReady,
            PhprDirectBlockedReason: directRuntime.BlockedReason,
            PaddleListenerStatus: paddleSnapshot.Status,
            PaddleMappingReady: paddleSnapshot.Mapping.LeftPaddleButtonId is not null
                && paddleSnapshot.Mapping.RightPaddleButtonId is not null,
            Bst1PaddleGearPulseEnabled: _bst1PaddleGearPulseEnabled,
            ShiftIntentEnabled: shiftIntentDiagnostics.IsEnabled));
    }
}

