using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Actuation.Driving;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Readiness;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Validation;
using HapticDrive.Simagic.PHPR.Output.Windows;
using HapticDrive.Asio.Runtime;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Telemetry;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using System.IO;
using System.Threading;

namespace HapticDrive.Asio.App;

public partial class MainWindow
{
    private async void TelemetryStatusTimer_Tick(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _telemetryStatusTickInFlight, 1) == 1)
        {
            Interlocked.Increment(ref _telemetryStatusTickSkippedCount);
            return;
        }

        try
        {
            RefreshPhprSoftwareCoexistenceStatus();
            var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
            await RouteMockPedalEffectsFromSnapshotAsync(pipelineSnapshot);
            RecordRoadTextureFlightRecorder(pipelineSnapshot);
            UpdateTelemetryStatus();
            UpdateHapticsControlState(pipelineSnapshot);
            UpdateMixerStatus();
            UpdateOutputStatus(pipelineSnapshot.Output);
            UpdateManualAsioHardwareTestStatus();
            UpdatePaddleInputStatus();
            UpdateShiftIntentStatus();
            UpdatePaddleGearBenchStatus();
            UpdateMockPedalEffectsStatus();
            UpdatePhprSoftwareCoexistenceStatus();
            UpdatePhprControlledWriteReadinessStatus();
            UpdateRealPhprDirectControlStatus();
            UpdatePhprPedalsStatus();
            UpdatePhprValidationStatus();
            PublishRuntimeControlSnapshot();
        }
        finally
        {
            Volatile.Write(ref _telemetryStatusTickInFlight, 0);
        }
    }

    private void RefreshPhprSoftwareCoexistenceStatus(bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force
            && _lastPhprCoexistenceScanUtc is not null
            && now - _lastPhprCoexistenceScanUtc.Value < TimeSpan.FromSeconds(5))
        {
            return;
        }

        _phprSoftwareCoexistenceSnapshot = _phprSoftwareCoexistenceDetector.Scan();
        _lastPhprCoexistenceScanUtc = now;
    }

    private PHprControlledWriteChecklist BuildStage2PControlledWriteChecklist()
    {
        return PHprControlledWriteChecklist.Stage2PNoWriteDefault with
        {
            HapticDriveRunning = true,
            EmergencyStopVisible = true,
            RealWritesDefaultOff = true,
            SimProClosed = _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Clear,
            SimHubClosed = _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Clear,
            SoftwareConflictStatus = _phprSoftwareCoexistenceSnapshot.Status
        };
    }

    private PHprManualValidationChecklist BuildPhprManualValidationChecklist()
    {
        var diagnostics = _realPhprOutput.GetDiagnostics();
        var options = diagnostics.Options;
        var authorization = _phprWriteAuthorization.Current;
        var canPulse = options.DirectControlEnabled
            && options.DirectControlArmed
            && !options.CandidateIsRawInputOnly
            && options.CandidateHasOpenableHidPath
            && options.OpenCheckSucceeded
            && options.AllowsDirectPulseReportShape
            && options.Selector.IsSelected
            && _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Clear
            && _outputInterlock.Current.AllowsOutput
            && authorization.IsAuthorized
            && !diagnostics.Output.IsEmergencyStopActive;

        return new PHprManualValidationChecklist(
            UserPhysicallyPresent: PhprValidationUserPresentCheckBox.IsChecked == true,
            P700Connected: PhprValidationP700ConnectedCheckBox.IsChecked == true,
            BrakeModuleInstalled: PhprValidationBrakeInstalledCheckBox.IsChecked == true,
            ThrottleModuleInstalled: PhprValidationThrottleInstalledCheckBox.IsChecked == true,
            DirectControlEnabled: options.DirectControlEnabled,
            DirectControlArmed: options.DirectControlArmed,
            DeviceInterfaceReportSelected: options.Selector.IsSelected,
            SafetyLimitsVisible: true,
            EmergencyStopVisible: true,
            EmergencyStopClear: !diagnostics.Output.IsEmergencyStopActive,
            BrakeTestPulseAvailable: canPulse && options.BrakeGearPulse.IsEnabled,
            ThrottleTestPulseAvailable: canPulse && options.ThrottleGearPulse.IsEnabled,
            GearPaddleTestPlanned: PhprValidationGearPaddlePlannedCheckBox.IsChecked == true,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status);
    }

    private PHprManualValidationResult BuildPhprManualValidationResult()
    {
        var selector = _realPhprOptions.Selector;
        return new PHprManualValidationResult(
            CreatedAtUtc: DateTimeOffset.UtcNow,
            AppBranchOrCommit: TryReadGitHeadSummary() ?? string.Empty,
            P700Connected: PhprValidationP700ConnectedCheckBox.IsChecked == true,
            BrakeModuleInstalled: PhprValidationBrakeInstalledCheckBox.IsChecked == true,
            ThrottleModuleInstalled: PhprValidationThrottleInstalledCheckBox.IsChecked == true,
            P700DeviceInfo: PhprValidationDeviceInfoTextBox.Text.Trim(),
            SimProStatus: FormatBoolUnknown(_phprSoftwareCoexistenceSnapshot.SimProRunning, _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Unknown),
            SimHubStatus: FormatBoolUnknown(_phprSoftwareCoexistenceSnapshot.SimHubRunning, _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Unknown),
            SelectedDeviceInterfaceReport: $"{(selector.IsSelected ? "selected" : "not selected")}; interface {selector.InterfaceName}; transport {selector.Transport}; report ID {FormatReportId(selector.ReportId)}; length {selector.ReportLength:N0} bytes",
            BrakeTestResult: PhprValidationBrakeResultTextBox.Text.Trim(),
            ThrottleTestResult: PhprValidationThrottleResultTextBox.Text.Trim(),
            EmergencyStopResult: PhprValidationEmergencyStopResultTextBox.Text.Trim(),
            PaddleUpshiftResult: PhprValidationUpshiftResultTextBox.Text.Trim(),
            PaddleDownshiftResult: PhprValidationDownshiftResultTextBox.Text.Trim(),
            WrongPedalBehavior: PhprValidationWrongPedalTextBox.Text.Trim(),
            SustainedVibrationBehavior: PhprValidationSustainedVibrationTextBox.Text.Trim(),
            Notes: PhprValidationNotesTextBox.Text.Trim(),
            PassFailDecision: PhprValidationPassFailDecisionTextBox.Text.Trim());
    }

    private async Task RouteMockPedalEffectsFromSnapshotAsync(HapticPipelineSnapshot pipelineSnapshot)
    {
        if (_routingMockPedalEffects)
        {
            return;
        }

        if (!_mockPedalEffectsRouter.GetSnapshot().Options.IsEnabled
            || pipelineSnapshot.VehicleStateUpdateCount <= 0
            || pipelineSnapshot.HapticFrame is null)
        {
            return;
        }

        _routingMockPedalEffects = true;
        try
        {
            await _mockPedalEffectsRouter.RouteAsync(
                pipelineSnapshot.HapticFrame,
                pipelineSnapshot.VehicleState,
                _lastActuationDrivingContext,
                BuildMockPedalEffectsSafetyContext(
                    telemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
                    hapticsStopped: !pipelineSnapshot.IsRunning));
        }
        finally
        {
            _routingMockPedalEffects = false;
        }
    }

    private bool IsRealPhprPedalRoutingReady(HapticPipelineSnapshot pipelineSnapshot)
    {
        return _realPhprOptions.DirectControlEnabled
            && _realPhprOptions.DirectControlArmed
            && !_realPhprOptions.CandidateIsRawInputOnly
            && _realPhprOptions.CandidateHasOpenableHidPath
            && _realPhprOptions.OpenCheckSucceeded
            && _realPhprOptions.AllowsDirectPulseReportShape
            && _realPhprOptions.Selector.IsSelected
            && _outputInterlock.Current.AllowsOutput
            && _phprWriteAuthorization.Current.IsAuthorized
            && pipelineSnapshot.HapticFrame is not null
            && pipelineSnapshot.VehicleStateUpdateCount > 0;
    }

    private PHprContinuousEffectsRuntimeInput BuildRealContinuousEffectsRuntimeInput()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        UpdateActuationTelemetryCaches(pipelineSnapshot);
        return new PHprContinuousEffectsRuntimeInput(
            pipelineSnapshot.HapticFrame,
            pipelineSnapshot.VehicleState,
            _lastActuationDrivingContext,
            IsRealPhprPedalRoutingReady(pipelineSnapshot),
            BuildRealRoadVibrationSafetyContext(
                telemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
                hapticsStopped: !pipelineSnapshot.IsRunning),
            BuildRealSlipLockSafetyContext(
                telemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
                hapticsStopped: !pipelineSnapshot.IsRunning));
    }

    private void PaddleInputSource_InputChanged(object? sender, object e)
    {
        _ = RunOnUiSafelyAsync("paddle-input-status-refresh", () =>
        {
            UpdatePaddleInputStatus();
            UpdateDiagnosticsStatus();
        });
    }

    private async void PaddleInputSource_PaddleInputReceived(object? sender, WheelPaddleInputEvent e)
    {
        var result = await _paddleInputRoutingCoordinator.HandleAsync(e);
        await RunOnUiSafelyAsync("paddle-input-route-status-refresh", () => ApplyPaddleInputRoutingUiUpdate(result));
    }

    private int GetEffectiveBst1PaddleGearDurationMs()
    {
        return Bst1GearPulseDurationSync.ResolveBst1Duration(
            _bst1PaddleGearSyncDuration,
            _sharedPhprGearPulseDurationMs,
            _bst1PaddleGearCustomDurationMs);
    }

    private bool BeginInvokeOnUiIfRequired(Action action)
    {
        return MainWindowUiDispatch.BeginInvokeIfRequired(
            new WpfMainWindowUiDispatcher(Dispatcher),
            action);
    }

    private ValueTask RunOnUiAsync(Action action)
    {
        return MainWindowUiDispatch.InvokeAsync(
            new WpfMainWindowUiDispatcher(Dispatcher),
            action);
    }

    private async Task RunOnUiSafelyAsync(
        string reason,
        Action action)
    {
        try
        {
            await RunOnUiAsync(action);
        }
        catch (Exception ex)
        {
            await _paddleInputRoutingCoordinator.HandleUiUpdateExceptionAsync(reason, ex);
        }
    }

    private Task<bool> ApplyPhprPedalsNormalOptionsFromControlsAsync(string footerMessage)
    {
        if (Dispatcher.CheckAccess())
        {
            return Task.FromResult(ApplyPhprPedalsNormalOptionsFromControls(footerMessage));
        }

        return Dispatcher.InvokeAsync(() => ApplyPhprPedalsNormalOptionsFromControls(footerMessage)).Task;
    }

    private PHprRealGearPulseSettings GetDeviceCardPulseSettings(PHprModuleId moduleId)
    {
        return moduleId == PHprModuleId.Throttle
            ? _realPhprOptions.ThrottleGearPulse
            : _realPhprOptions.BrakeGearPulse;
    }

    private void TelemetryReceiver_PacketReceived(object? sender, UdpTelemetryPacketReceivedEventArgs e)
    {
        _telemetrySessionController.Enqueue(_telemetryIngressWorker, e.Packet);
    }

    private HapticPipelineSnapshot RefreshDrivingArmedAndShiftIntentTelemetry()
    {
        var snapshot = _hapticPipeline.GetSnapshot();
        UpdateActuationTelemetryCaches(snapshot);
        return snapshot;
    }

    private void UpdateActuationTelemetryCaches(HapticPipelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var nowUtc = DateTimeOffset.UtcNow;
        var telemetryAge = snapshot.TelemetryFreshness.Age
            ?? snapshot.TelemetryAge
            ?? CalculateTelemetryAge(snapshot.LastVehicleStateUpdateAtUtc, nowUtc);

        _shiftIntentProcessor.UpdateTelemetry(
            snapshot.HapticFrame,
            snapshot.VehicleState,
            snapshot.LastVehicleStateUpdateAtUtc,
            telemetryAge);

        if (snapshot.HapticFrame is null)
        {
            _lastDrivingArmedState = DrivingArmedState.NotArmed(
                "No canonical haptic frame is available for actuation routing.",
                nowUtc,
                telemetryAge);
            _lastActuationDrivingContext = ActuationDrivingContextFactory.SafeDefault(
                snapshot.VehicleState.Frame.Source ?? snapshot.InputSource.ToString(),
                nowUtc);
            return;
        }

        var drivingState = _drivingArmedStateService.UpdateFromHapticFrame(
            snapshot.HapticFrame,
            snapshot.VehicleState,
            BuildDrivingArmedEvaluationContext(snapshot, telemetryAge),
            nowUtc);
        _lastDrivingArmedState = drivingState;
        _lastActuationDrivingContext = ActuationDrivingContextFactory.FromHapticFrame(
            snapshot.HapticFrame,
            drivingState.IsArmed);
    }

    private static DrivingArmedEvaluationContext BuildDrivingArmedEvaluationContext(
        HapticPipelineSnapshot snapshot,
        TimeSpan? telemetryAge)
    {
        return new DrivingArmedEvaluationContext
        {
            HapticsRunning = snapshot.IsRunning,
            EmergencyMute = snapshot.EmergencyMute,
            HasRecentTelemetry = snapshot.VehicleStateUpdateCount > 0,
            LastVehicleStateUpdateAtUtc = snapshot.LastVehicleStateUpdateAtUtc,
            TelemetryAge = telemetryAge,
            TelemetryTimedOutMuted = snapshot.TelemetryTimedOutMuted
                || (snapshot.TelemetryFreshness.IsPresent && !snapshot.TelemetryFreshness.IsFresh)
        };
    }

    private static TimeSpan? CalculateTelemetryAge(DateTimeOffset? lastVehicleStateUpdateAtUtc, DateTimeOffset nowUtc)
    {
        if (lastVehicleStateUpdateAtUtc is null)
        {
            return null;
        }

        var age = nowUtc - lastVehicleStateUpdateAtUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private WheelPaddleMapping GetPaddleMapping()
    {
        return _paddleMapping;
    }

    private void NotifyAcceptedGearPulse(DateTimeOffset acceptedAtUtc)
    {
        _hapticPipeline.NotifyLocalGearPulseAccepted(acceptedAtUtc);
        _realRoadVibrationRouter.NotifyGearPulseAccepted(acceptedAtUtc);
        _realSlipLockRouter.NotifyGearPulseAccepted(acceptedAtUtc);
    }

    private Bst1PaddleGearPulseRouteSettings BuildBst1PaddleGearPulseRouteSettings()
    {
        return new Bst1PaddleGearPulseRouteSettings(
            _bst1PaddleGearPulseEnabled,
            _bst1PaddleGearStrengthPercent,
            _bst1OutputTrimPercent,
            _bst1PaddleGearFrequencyHz,
            GetEffectiveBst1PaddleGearDurationMs(),
            _bst1PaddleGearSyncDuration ? "sync" : "custom");
    }

    private void ApplyPaddleInputRoutingUiUpdate(PaddleInputRoutingHandleResult result)
    {
        if (result.RealRoutingResult is not null)
        {
            _lastRealPhprGearPulseRoutingResult = result.RealRoutingResult;
        }

        if (!string.IsNullOrWhiteSpace(result.Bst1PaddleGearPulseMessage))
        {
            _lastBst1PaddleGearPulseMessage = result.Bst1PaddleGearPulseMessage;
        }

        if (result.FailedSafely)
        {
            UpdatePaddleGearBenchStatus();
            UpdateRealPhprDirectControlStatus();
            UpdatePhprValidationStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = "Paddle input routing failed safely; P-HPR Stop All recovery was attempted when needed.";
            return;
        }

        UpdateShiftIntentStatus();
        UpdatePaddleGearBenchStatus();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprValidationStatus();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = BuildPaddleInputFooterStatusText(result);
    }

    private string BuildPaddleInputFooterStatusText(PaddleInputRoutingHandleResult result)
    {
        var shiftIntentResult = result.ShiftIntentResult;
        if (shiftIntentResult is null)
        {
            return "Paddle input routing completed without a shift-intent result.";
        }

        var realRoutingResult = result.RealRoutingResult;
        var realMessage = realRoutingResult is not null
            && (_realPhprOptions.DirectControlEnabled || realRoutingResult.Routed)
                ? $" {realRoutingResult.Message}"
                : string.Empty;
        return result.MockRoutingResult is null
            ? $"{shiftIntentResult.Message}{realMessage}{FormatBenchRoutingFooter(result.BenchResult, result.BenchRoutingMessage)}"
            : $"{shiftIntentResult.Message} {result.MockRoutingResult.Message}{realMessage}{FormatBenchRoutingFooter(result.BenchResult, result.BenchRoutingMessage)}";
    }

    private static string FormatBenchRoutingFooter(
        PaddleGearBenchTestResult? benchResult,
        string? routingMessage)
    {
        if (benchResult is null)
        {
            return string.Empty;
        }

        if (benchResult.Accepted)
        {
            return string.IsNullOrWhiteSpace(routingMessage)
                ? " Bench event accepted."
                : $" {routingMessage}";
        }

        return $" {benchResult.Message}";
    }

    private PHprSafetyContext BuildMockGearPulseSafetyContext(ShiftIntentEvent shiftIntentEvent)
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        return BuildMockPhprSafetyContext(
            _lastActuationDrivingContext with { IsArmed = shiftIntentEvent.DrivingArmedAtEvent.IsArmed },
            pipelineSnapshot.TelemetryTimedOutMuted,
            hapticsStopped: !pipelineSnapshot.IsRunning);
    }

    private PHprSafetyContext BuildMockPedalEffectsSafetyContext(
        bool telemetryStale,
        bool hapticsStopped)
    {
        return BuildMockPhprSafetyContext(_lastActuationDrivingContext, telemetryStale, hapticsStopped);
    }

    private PHprSafetyContext BuildRealGearPulseSafetyContext(ShiftIntentEvent shiftIntentEvent)
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
                outputSnapshot,
                pipelineSnapshot.TelemetryTimedOutMuted,
                hapticsStopped: !pipelineSnapshot.IsRunning,
                _emergencyMuted,
                shiftIntentEvent.DrivingArmedAtEvent.IsArmed,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildPaddleGearBenchMockSafetyContext()
    {
        var outputSnapshot = _mockPhprSafetyOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildBenchMockSnapshot(
                outputSnapshot,
                _emergencyMuted)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildPaddleGearBenchDirectSafetyContext()
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildBenchDirectSnapshot(
                outputSnapshot,
                _emergencyMuted,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildRealRoadVibrationSafetyContext(
        bool telemetryStale,
        bool hapticsStopped)
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
                outputSnapshot,
                telemetryStale,
                hapticsStopped,
                _emergencyMuted,
                _lastActuationDrivingContext.IsArmed,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildRealSlipLockSafetyContext(
        bool telemetryStale,
        bool hapticsStopped)
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
                outputSnapshot,
                telemetryStale,
                hapticsStopped,
                _emergencyMuted,
                _lastActuationDrivingContext.IsArmed,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildManualRealPhprSafetyContext()
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildManualRealSnapshot(
                _realPhprOptions.Selector.IsSelected,
                _emergencyMuted,
                outputSnapshot.IsEmergencyStopActive,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildMockPhprSafetyContext(
        ActuationDrivingContext drivingContext,
        bool telemetryStale,
        bool hapticsStopped)
    {
        var outputSnapshot = _mockPhprSafetyOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildMockRuntimeSnapshot(
                outputSnapshot,
                telemetryStale,
                hapticsStopped,
                _emergencyMuted,
                drivingContext.IsArmed,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private void UpdateTelemetryStatus()
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        if (_telemetryStartError is not null)
        {
            TelemetryStatusText.Text = "UDP: unavailable";
            TelemetryStatusText.ToolTip = "Telemetry listener unavailable.";
            UpdateEffectStatus();
            UpdateRecordingStatus();
            UpdateDiagnosticsStatus();
            UpdateDashboardStatus(pipelineSnapshot);
            return;
        }

        var snapshot = _telemetryReceiver.GetSnapshot();
        var status = snapshot.HasNoPacketWarning
            ? "No packets yet"
            : snapshot.IsRunning
                ? "Listening"
                : "Stopped";

        var telemetrySourceSummary = _allowLanTelemetry
            ? _allowedTelemetryRemoteAddresses.Count > 0
                ? "LAN allowlist"
                : "LAN warning"
            : "Loopback";
        TelemetryStatusText.Text = $"UDP: {status} · {telemetrySourceSummary}";
        TelemetryStatusText.ToolTip = BuildTelemetryUdpStatusPresentation(
            pipelineSnapshot,
            snapshot,
            _telemetryIngressWorker.GetSnapshot(),
            status).ListenerDetailText;
        UpdateEffectStatus();
        UpdateRecordingStatus();
        UpdateDiagnosticsStatus();
        UpdateDashboardStatus(pipelineSnapshot);

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Telemetry / UDP" })
        {
            PageStatusText.Text = BuildTelemetryUdpStatusPresentation(
                pipelineSnapshot,
                snapshot,
                _telemetryIngressWorker.GetSnapshot(),
                status).TelemetryUdpPageStatusText;
        }
    }

    private void UpdateEffectStatus()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var presentation = BuildEffectsStatusPresentation(pipelineSnapshot);
        EffectsViewControl.Apply(presentation);

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Effects" })
        {
            PageStatusText.Text = presentation.EffectsPageStatusText;
        }

        UpdateDiagnosticsStatus();
    }

    private EffectsStatusPresentation BuildEffectsStatusPresentation(HapticPipelineSnapshot pipelineSnapshot)
    {
        return EffectsStatusPresenter.Build(
            EffectsStatusSnapshotBuilder.Build(
                pipelineSnapshot.Effects,
                _hapticPipeline.EffectEngine.Options));
    }

    private void UpdateRecordingStatus()
    {
        UpdateTelemetryUdpPresentation();
        UpdateDashboardStatus();
        UpdateDiagnosticsStatus();
    }

    private void UpdateTelemetryUdpPresentation()
    {
        var presentation = BuildTelemetryUdpStatusPresentation();
        TelemetryUdpViewControl.Apply(presentation);
        _telemetrySessionController.Publish(
            presentation,
            _allowLanTelemetry,
            _allowedTelemetryRemoteAddresses,
            _telemetryListenerWarning);
        _recordingReplayController.Publish(presentation);
    }

    private void UpdateProfilesPresentation(string? message = null, IReadOnlyList<string>? validationMessages = null)
    {
        ProfilesViewControl.Apply(BuildProfilesStatusPresentation(message, validationMessages));
    }

    private void UpdateTestingValidationPresentation()
    {
        TestingValidationViewControl.Apply(BuildTestingValidationStatusPresentation());
    }

    private TelemetryUdpStatusPresentation BuildTelemetryUdpStatusPresentation()
    {
        return BuildTelemetryUdpStatusPresentation(
            _hapticPipeline.GetSnapshot(),
            _telemetryReceiver.GetSnapshot(),
            _telemetryIngressWorker.GetSnapshot(),
            GetTelemetryListenerPageStatus());
    }

    private TelemetryUdpStatusPresentation BuildTelemetryUdpStatusPresentation(
        HapticPipelineSnapshot pipelineSnapshot,
        UdpTelemetryReceiverSnapshot receiverSnapshot,
        TelemetryIngressWorkerSnapshot ingressSnapshot,
        string listenerStatusText)
    {
        var recordingSnapshot = pipelineSnapshot.Recording;
        var replaySnapshot = pipelineSnapshot.Replay;
        var replayMode = GetSelectedReplayTimingMode();
        var forwardingSnapshot = pipelineSnapshot.Forwarding;

        return TelemetryUdpStatusPresenter.Build(new TelemetryUdpStatusSnapshot(
            ReplayTimingModeHelpText: replayMode.HelpText,
            RecordingActive: recordingSnapshot.IsRecording,
            RecordingFileName: recordingSnapshot.FilePath is null ? string.Empty : Path.GetFileName(recordingSnapshot.FilePath),
            RecordingLastPacketRelativeTime: recordingSnapshot.LastPacketRelativeTime,
            RecordingError: _recordingError ?? recordingSnapshot.LastErrorMessage ?? ingressSnapshot.LastErrorMessage,
            ReplayActive: replaySnapshot.IsReplaying,
            ReplayModeLabel: replayMode.Label,
            ReplaySourceFileName: replaySnapshot.SourceFilePath is null ? string.Empty : Path.GetFileName(replaySnapshot.SourceFilePath),
            ReplayPacketCount: replaySnapshot.PacketsReplayed,
            ReplayStatusMessage: replaySnapshot.StatusMessage,
            ReplayError: _replayError,
            ListenerStatusText: listenerStatusText,
            ListenerPort: receiverSnapshot.BoundPort,
            AllowLanTelemetry: _allowLanTelemetry,
            HasAllowedRemoteAddresses: _allowedTelemetryRemoteAddresses.Count > 0,
            LanWarningText: _telemetryListenerWarning,
            ReceivedPacketCount: ingressSnapshot.ReceivedPacketCount,
            HapticDroppedPacketCount: ingressSnapshot.HapticDroppedPacketCount,
            ForwardingDroppedPacketCount: ingressSnapshot.ForwardingDroppedPacketCount,
            IgnoredRemotePacketCount: receiverSnapshot.IgnoredRemotePacketCount,
            OversizedDatagramCount: receiverSnapshot.OversizedDatagramCount,
            ForwardedDatagramCount: forwardingSnapshot.ForwardedDatagramCount,
            RecordingPacketCount: recordingSnapshot.PacketCount,
            RecordingQueuedPacketCount: recordingSnapshot.QueuedPacketCount,
            RecordingQueueCapacityPackets: recordingSnapshot.QueueCapacityPackets,
            RecordingDroppedPacketCount: recordingSnapshot.DroppedPacketCount + ingressSnapshot.RecordingDroppedPacketCount,
            RecordingIncomplete: recordingSnapshot.RecordingIncomplete || ingressSnapshot.RecordingMarkedIncomplete,
            ParserSuccessCount: pipelineSnapshot.ParserSuccessCount,
            VehicleStateUpdateCount: pipelineSnapshot.VehicleStateUpdateCount,
            ForwardingDestinationCount: _forwardingDestinations.Count,
            ForwardingEnabledDestinationCount: _forwardingDestinations.Count(destination => destination.Enabled),
            ListenerDefaultPort: UdpTelemetryReceiverOptions.DefaultPort));
    }

    private string GetTelemetryListenerPageStatus()
    {
        if (_telemetryStartError is not null)
        {
            return "unavailable";
        }

        var snapshot = _telemetryReceiver.GetSnapshot();
        return snapshot.HasNoPacketWarning
            ? "No packets yet"
            : snapshot.IsRunning
                ? "Listening"
                : "Stopped";
    }

    private ProfilesStatusPresentation BuildProfilesStatusPresentation(
        string? message = null,
        IReadOnlyList<string>? validationMessages = null)
    {
        return ProfilesStatusPresenter.Build(new ProfilesStatusSnapshot(
            CurrentProfileName: _currentProfile.Name,
            StatusMessage: message,
            ValidationMessages: validationMessages ?? [],
            AudioProfilePath: HapticProfileStore.GetDefaultProfilePath(),
            PhprProfilePath: PhprEffectProfileStore.GetDefaultProfilePath(),
            AudioProfileVersion: HapticDriveProfile.CurrentVersion,
            PhprProfileVersion: PhprEffectProfile.CurrentVersion));
    }

    private TestingValidationStatusPresentation BuildTestingValidationStatusPresentation()
    {
        var snapshot = _testBench.GetSnapshot();
        return TestingValidationStatusPresenter.Build(new TestingValidationStatusSnapshot(
            TestBenchActive: snapshot.IsActive,
            TestBenchEmergencyMute: snapshot.EmergencyMute,
            TestBenchSelectedSignalName: snapshot.SelectedSignalName,
            TestBenchOutputPeakLevel: snapshot.OutputPeakLevel,
            TestBenchLimitedSampleCount: snapshot.LimitedSampleCount,
            TestBenchOutputDisplayName: snapshot.OutputDisplayName,
            TestBenchOutputState: snapshot.OutputState.ToString()));
    }

    private ReplayTimingModeOption GetSelectedReplayTimingMode()
    {
        return ControlSettingsSnapshotBuilder.GetSelectedReplayTimingMode(
            ReplayTimingModeComboBox.SelectedItem as ReplayTimingModeOption);
    }

    private ReplayTimingPreference GetSelectedReplayTimingPreference()
    {
        return ControlSettingsSnapshotBuilder.GetReplayTimingPreference(
            ReplayTimingModeComboBox.SelectedItem as ReplayTimingModeOption);
    }
}
