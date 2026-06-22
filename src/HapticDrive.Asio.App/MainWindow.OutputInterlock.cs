using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using System.Windows;
using System.Windows.Input;

namespace HapticDrive.Asio.App;

public partial class MainWindow
{
    private async void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        _outputInterlock.Trip(
            OutputInterlockReason.UserEmergencyMute,
            "Emergency mute requested from the main window.");
        await ApplyOutputInterlockChangeAsync(
            "Global output interlock latched across ASIO, test bench, and P-HPR routing.");
    }

    private async void ResetOutputInterlockButton_Click(object sender, RoutedEventArgs e)
    {
        var resetResult = await TryResetOutputInterlockAsync();
        FooterStatusText.Text = resetResult;
    }

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        if (e.Key == Key.M)
        {
            e.Handled = true;
            _outputInterlock.Trip(
                OutputInterlockReason.UserEmergencyMute,
                "Emergency mute requested from the keyboard shortcut.");
            await ApplyOutputInterlockChangeAsync(
                "Global output interlock latched across ASIO, test bench, and P-HPR routing.");
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
        var outputStatus = _hapticPipeline.GetSnapshot().Output;
        if (!_hapticsStarted || !_hapticPipeline.GetSnapshot().IsRunning)
        {
            return "Output interlock reset blocked: start haptics first so the runtime and safety path are active.";
        }

        if (!IsOutputConfigurationValidForInterlockReset(outputStatus))
        {
            return "Output interlock reset blocked: select a valid output configuration before enabling output.";
        }

        if (!_outputInterlock.Current.IsLatched)
        {
            return "Output interlock is already reset.";
        }

        if (!_outputInterlock.Reset("Output interlock reset from the main window after readiness checks passed."))
        {
            return "Output interlock reset was ignored because the latch state did not change.";
        }

        await ApplyOutputInterlockChangeAsync(
            "Global output interlock reset; output may resume when fresh signals and routing allow it.");
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

    private void SyncOutputInterlockState(OutputInterlockSnapshot snapshot)
    {
        _applicationSafetyController.Publish(snapshot);
        _emergencyMuted = snapshot.IsLatched;
        _testBench.EmergencyMute = _emergencyMuted;
        SafetyStatusText.Text = $"Safety: {_safetyStateViewModel.StatusText}";
        SafetyStatusText.ToolTip = string.IsNullOrWhiteSpace(_safetyStateViewModel.Message)
            ? _safetyStateViewModel.StatusText
            : $"{_safetyStateViewModel.StatusText}. {_safetyStateViewModel.Message}";
        if (ResetOutputInterlockButton is not null)
        {
            ResetOutputInterlockButton.IsEnabled = snapshot.IsLatched;
        }
    }

    private bool IsOutputConfigurationValidForInterlockReset(AudioOutputStatus outputStatus)
    {
        if (_selectedOutputKind == AudioOutputDeviceKind.Null)
        {
            return true;
        }

        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            return !string.IsNullOrWhiteSpace(_selectedAsioDriverName)
                && outputStatus.State != AudioOutputDeviceState.Faulted;
        }

        return outputStatus.State != AudioOutputDeviceState.Faulted;
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
    }
}
