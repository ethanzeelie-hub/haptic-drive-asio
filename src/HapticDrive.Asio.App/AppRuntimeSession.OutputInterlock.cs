using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Asio.Core.Safety;
using System.Windows;
using System.Windows.Input;

namespace HapticDrive.Asio.App;

internal sealed partial class AppRuntimeSession
{
    internal async void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        FooterStatusText.Text = await TripEmergencyMuteAsync("Emergency mute requested from the main window.");
    }

    internal async void ResetOutputInterlockButton_Click(object sender, RoutedEventArgs e)
    {
        var resetResult = await TryResetOutputInterlockAsync();
        FooterStatusText.Text = resetResult;
    }

    internal async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        if (e.Key == Key.M)
        {
            e.Handled = true;
            FooterStatusText.Text = await TripEmergencyMuteAsync("Emergency mute requested from the keyboard shortcut.");
            return;
        }

        if (e.Key == Key.R)
        {
            e.Handled = true;
            FooterStatusText.Text = await TryResetOutputInterlockAsync();
        }
    }

    private async Task<string> TryResetOutputInterlockAsync()
    {
        if (!_outputInterlock.Current.IsLatched)
        {
            return "Output interlock is already reset.";
        }

        if (_applicationSafetyController.TryBuildResetBlockedMessage(
            _outputInterlockSupervisor,
            out var resetBlockedMessage))
        {
            return PublishOutputInterlockResetFailure(resetBlockedMessage);
        }

        if (!_outputInterlock.Reset("Output interlock reset from the main window after readiness checks passed."))
        {
            return PublishOutputInterlockResetFailure("Output interlock reset was ignored because the latch state did not change.");
        }

        await ApplyOutputInterlockChangeAsync(
            "Global output interlock reset; output may resume when fresh signals and routing allow it.");
        return FooterStatusText.Text;
    }

    private async Task ApplyStartupOutputInterlockPlanAsync()
    {
        var snapshot = _phprDirectRuntime.GetSnapshot();
        var canReset = !_applicationSafetyController.TryBuildResetBlockedMessage(
            _outputInterlockSupervisor,
            out var resetBlockedMessage);
        var plan = StartupOutputInterlockPlanner.Build(new StartupOutputInterlockSnapshot(
            InterlockIsLatched: _outputInterlock.Current.IsLatched,
            InterlockReason: _outputInterlock.Current.Reason,
            CanReset: canReset,
            ResetBlocker: resetBlockedMessage,
            UncleanShutdownMarkerExists: snapshot.UncleanShutdownMarkerExists,
            DisabledAfterUncleanShutdown: snapshot.DisabledAfterUncleanShutdown));

        if (plan.ShouldEnableOutput
            && _outputInterlock.Current.IsLatched
            && _outputInterlock.Reset("Startup readiness checks passed. Output enabled without starting live haptics or sending startup output."))
        {
            await ApplyOutputInterlockChangeAsync(plan.FooterMessage);
            return;
        }

        SyncOutputInterlockState(_outputInterlock.Current, plan.StatusMessage);
        UpdateManualAsioHardwareTestStatus();
        UpdatePhprPedalsStatus();
        FooterStatusText.Text = plan.FooterMessage;
    }

    private async Task<string> TripEmergencyMuteAsync(string requestMessage)
    {
        if (_outputInterlock.Current.IsLatched
            && _outputInterlock.Current.Reason == OutputInterlockReason.UserEmergencyMute)
        {
            var alreadyActiveMessage = "Emergency mute is already active. Use Reset Output Interlock after outputs are silent and safe.";
            SyncOutputInterlockState(_outputInterlock.Current, alreadyActiveMessage);
            UpdateManualAsioHardwareTestStatus();
            UpdatePhprPedalsStatus();
            return alreadyActiveMessage;
        }

        _outputInterlock.Trip(
            OutputInterlockReason.UserEmergencyMute,
            requestMessage);
        await ApplyOutputInterlockChangeAsync(
            "Global output interlock latched across ASIO, test bench, and P-HPR routing. Use Reset Output Interlock after outputs are silent and safe.");
        return FooterStatusText.Text;
    }

    private async Task ApplyOutputInterlockChangeAsync(string footerMessage)
    {
        SyncOutputInterlockState(_outputInterlock.Current);
        var pipelineMuteResult = await _hapticPipeline.SetEmergencyMuteAsync(_emergencyMuted);
        await SyncGlobalPhprOutputInterlockAsync(_outputInterlock.Current);
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        UpdateHapticsControlState(pipelineSnapshot);

        if (_hapticsStarted && !pipelineMuteResult.Succeeded)
        {
            FooterStatusText.Text = pipelineMuteResult.Message;
            return;
        }

        if (_testBench.GetSnapshot().IsActive)
        {
            var testBenchResult = await _testBench.RenderNextBufferAsync();
            if (!testBenchResult.Succeeded)
            {
                FooterStatusText.Text = testBenchResult.Message;
                UpdateTestBenchStatus();
                return;
            }
        }

        FooterStatusText.Text = footerMessage;
        UpdateEffectStatus();
        UpdateMixerStatus();
        UpdateTestBenchStatus();
        UpdateManualAsioHardwareTestStatus();
        UpdateDiagnosticsStatus();
        UpdateDeviceStatus();
    }

    private string PublishOutputInterlockResetFailure(string message)
    {
        SyncOutputInterlockState(_outputInterlock.Current, message);
        UpdateManualAsioHardwareTestStatus();
        UpdatePhprPedalsStatus();
        PublishDiagnosticEvent(
            "safety.interlock-reset-failure",
            DiagnosticSeverity.Warning,
            "Safety",
            message,
            _diagnosticCorrelationContext.Current.AppSessionId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["interlockGeneration"] = _outputInterlock.Current.Generation.ToString()
            });
        return message;
    }

    private void SyncOutputInterlockState(OutputInterlockSnapshot snapshot, string? messageOverride = null)
    {
        _applicationSafetyController.Publish(snapshot, messageOverride);
        _emergencyMuted = snapshot.IsLatched;
        _testBench.EmergencyMute = _emergencyMuted;
        SafetyStatusText.Text = $"Safety: {_safetyStateViewModel.StatusText}";
        SafetyStatusText.ToolTip = string.IsNullOrWhiteSpace(_safetyStateViewModel.Message)
            ? _safetyStateViewModel.StatusText
            : $"{_safetyStateViewModel.StatusText}. {_safetyStateViewModel.Message}";
        if (ResetOutputInterlockButton is not null)
        {
            ResetOutputInterlockButton.IsEnabled = snapshot.IsLatched;
            ResetOutputInterlockButton.ToolTip = snapshot.IsLatched
                ? $"Reset the global output interlock. {_safetyStateViewModel.Message}"
                : "Global output interlock is already clear.";
        }
    }

    private async Task SyncGlobalPhprOutputInterlockAsync(OutputInterlockSnapshot snapshot)
    {
        if (snapshot.IsLatched)
        {
            RevokePhprWriteAuthorization($"Global output interlock latched: {snapshot.Reason}.");
            await _mockGearPulseRouter.EmergencyStopAsync();
            await _mockPedalEffectsRouter.EmergencyStopAsync();
            await _phprDirectRuntime.EmergencyStopAsync(snapshot.Message);
            return;
        }

        _mockGearPulseRouter.ClearEmergencyStop();
        _mockPedalEffectsRouter.ClearEmergencyStop();
        _phprDirectRuntime.ClearEmergencyStop();
        RestoreOwnerLocalPhprWriteAuthorization("global output interlock cleared");
    }
}
