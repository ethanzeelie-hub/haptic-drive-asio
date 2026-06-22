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

public partial class MainWindow : Window
{
    private async void OutputModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputUi
            || OutputModeComboBox.SelectedItem is not OutputModeOption option)
        {
            return;
        }

        _selectedOutputKind = option.Kind;
        SaveAppSettings();
        await RunSerializedLifecycleOperationAsync(
            (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                generation,
                $"Output mode changed to {option.Label}; haptics are stopped until started explicitly.",
                cancellationToken),
            "Output mode change failed");
    }

    private async void AsioDriverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputUi)
        {
            return;
        }

        _selectedAsioDriverName = AsioDriverComboBox.SelectedItem as string;
        SaveAppSettings();
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            await RunSerializedLifecycleOperationAsync(
                (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                    generation,
                    "ASIO driver selection changed; haptics are stopped until ASIO is armed and started explicitly.",
                    cancellationToken),
                "ASIO driver change failed");
        }
    }

    private async void AsioOutputChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputUi)
        {
            return;
        }

        _selectedAsioOutputChannel = AsioOutputChannelComboBox.SelectedItem is int channel
            ? channel
            : null;
        SaveAppSettings();
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            await RunSerializedLifecycleOperationAsync(
                (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                    generation,
                    "ASIO channel selection changed; haptics are stopped until started explicitly.",
                    cancellationToken),
                "ASIO channel change failed");
        }
    }

    private async void AsioArmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingOutputUi)
        {
            return;
        }

        _asioArmed = AsioArmCheckBox.IsChecked == true;
        SaveAppSettings();
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            await RunSerializedLifecycleOperationAsync(
                (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                    generation,
                    _asioArmed
                        ? "ASIO armed. Start Haptics is still required before output can run."
                        : "ASIO disarmed and haptics stopped.",
                    cancellationToken),
                "ASIO arm state update failed");
        }
    }

    private async void RefreshAsioButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsioVisibilityDiagnosticsAsync();
        FooterStatusText.Text = "ASIO readiness diagnostics refreshed.";
    }

    private async void RefreshInputDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshInputDeviceDiscoveryAsync(isStartupRefresh: false);
    }

    private async Task RefreshInputDeviceDiscoveryAsync(bool isStartupRefresh = false)
    {
        InputDiscoveryStatusText.Text = "Input discovery is refreshing read-only Windows metadata.";
        InputDiscoveryItemsControl.ItemsSource = new[]
        {
            "Safety: discovery only. No live paddle listener, haptic routing, or P-HPR output command is started."
        };

        try
        {
            _inputDiscoverySnapshot = await _inputDeviceDiscovery.DiscoverAsync();
            UpdatePaddleDeviceSelectionItems();
            UpdateInputDiscoveryStatus();
            UpdatePaddleInputStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = isStartupRefresh
                ? $"Input device discovery refreshed on startup; {_inputDiscoverySnapshot.DeviceCount:N0} device(s) found. Listener remains stopped."
                : $"Input device discovery refreshed; {_inputDiscoverySnapshot.DeviceCount:N0} device(s) found. No commands were sent.";
        }
        catch (Exception ex)
        {
            _inputDiscoverySnapshot = InputDeviceDiscoverySnapshot.Create(
                [],
                [],
                [$"Input discovery failed before any device commands were sent: {ex.Message}"]);
            UpdatePaddleDeviceSelectionItems();
            UpdateInputDiscoveryStatus();
            UpdatePaddleInputStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = "Input device discovery failed safely.";
        }
    }

    private async void StartPaddleInputListenerButton_Click(object sender, RoutedEventArgs e)
    {
        await StartPaddleInputListenerForWorkflowAsync("manual listener");
    }

    private async Task<bool> StartPaddleInputListenerForWorkflowAsync(string workflowName)
    {
        if (!TryBuildPaddleMappingFromControls(out var mapping, out var message))
        {
            PaddleInputStatusText.Text = message;
            FooterStatusText.Text = message;
            return false;
        }

        if (PaddleInputDeviceComboBox.SelectedItem is not PaddleDeviceListItem deviceItem)
        {
            PaddleInputStatusText.Text = "Select a Windows game-controller input device before starting the read-only paddle listener.";
            FooterStatusText.Text = "Paddle input listener was not started; no input device is selected.";
            return false;
        }

        if (!PaddleInputDeviceSelector.HasUsableButtons(deviceItem.Device))
        {
            var blocked = $"Selected Windows game-controller reports {PaddleInputDeviceSelector.GetUsableButtonCount(deviceItem.Device):N0} usable buttons. Select the 32-button VID_3670/PID_0905 wheel input device before starting the listener.";
            PaddleInputStatusText.Text = blocked;
            FooterStatusText.Text = $"Paddle input listener was not started; {blocked}";
            return false;
        }

        _paddleMapping = mapping with
        {
            SelectedDeviceId = deviceItem.Selection.DeviceId,
            SelectedMethod = deviceItem.Selection.Method
        };
        SaveAppSettings();
        await _paddleInputSource.StartAsync(deviceItem.Selection, _paddleMapping);
        UpdatePaddleInputStatus();
        UpdateLocalGearTestStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = workflowName == "local gear test"
            ? "Read-only paddle listener started for Local Gear Test Mode. Start Haptics and F1 telemetry were not started."
            : "Read-only paddle input listener started. Mapped presses now update the status view only.";
        return true;
    }

    private async void StopPaddleInputListenerButton_Click(object sender, RoutedEventArgs e)
    {
        await _paddleInputSource.StopAsync();
        UpdatePaddleInputStatus();
        UpdateLocalGearTestStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Read-only paddle input listener stopped.";
    }

    private void SetLeftPaddleFromLastChangedButton_Click(object sender, RoutedEventArgs e)
    {
        AssignPaddleFromLastChangedButton(PaddleSide.Left);
    }

    private void SetRightPaddleFromLastChangedButton_Click(object sender, RoutedEventArgs e)
    {
        AssignPaddleFromLastChangedButton(PaddleSide.Right);
    }

    private void ShiftIntentEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingShiftIntentUi)
        {
            return;
        }

        ApplyShiftIntentOptionsFromControls("Shift intent preferences updated.");
    }

    private void ShiftIntentModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingShiftIntentUi)
        {
            return;
        }

        ApplyShiftIntentOptionsFromControls("Shift intent mode updated.");
    }

    private void ClearShiftIntentDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        _shiftIntentProcessor.ClearDiagnostics();
        UpdateShiftIntentStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Shift intent diagnostics cleared.";
    }

    private void PaddleGearBenchControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingPaddleGearBenchUi)
        {
            return;
        }

        ApplyPaddleGearBenchOptionsFromControls("Paddle Gear Bench Test settings updated for this session only.");
    }

    private void PaddleGearBenchControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPaddleGearBenchUi)
        {
            return;
        }

        ApplyPaddleGearBenchOptionsFromControls("Paddle Gear Bench Test settings updated for this session only.");
    }

    private void PaddleGearBenchControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPaddleGearBenchUi)
        {
            return;
        }

        ApplyPaddleGearBenchOptionsFromControls("Paddle Gear Bench Test profile updated for this session only.");
    }

    private void ClearPaddleGearBenchCountersButton_Click(object sender, RoutedEventArgs e)
    {
        _paddleGearBenchTestController.ClearDiagnostics();
        UpdatePaddleGearBenchStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Paddle Gear Bench Test counters cleared.";
    }

    private async void LocalGearTestModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingPaddleGearBenchUi)
        {
            return;
        }

        _localGearTestModeEnabled = LocalGearTestModeCheckBox.IsChecked == true;
        _localGearTestAutoStartListener = LocalGearTestAutoStartListenerCheckBox.IsChecked == true;
        _updatingPaddleGearBenchUi = true;
        PaddleGearBenchEnabledCheckBox.IsChecked = _localGearTestModeEnabled;
        PaddleGearBenchArmCheckBox.IsChecked = _localGearTestModeEnabled;
        _updatingPaddleGearBenchUi = false;
        ApplyPaddleGearBenchOptionsFromControls(
            _localGearTestModeEnabled
                ? "Local Gear Test Mode enabled. Start Haptics and F1 telemetry are not required."
                : "Local Gear Test Mode disabled.");

        if (_localGearTestModeEnabled && _localGearTestAutoStartListener)
        {
            var readiness = EvaluateLocalGearTestReadiness();
            if (readiness.CanStartListener)
            {
                await StartPaddleInputListenerForWorkflowAsync("local gear test");
            }
        }

        UpdateLocalGearTestStatus();
    }

    private async void StartGearTestListenerButton_Click(object sender, RoutedEventArgs e)
    {
        _localGearTestModeEnabled = true;
        _updatingPaddleGearBenchUi = true;
        LocalGearTestModeCheckBox.IsChecked = true;
        PaddleGearBenchEnabledCheckBox.IsChecked = true;
        PaddleGearBenchArmCheckBox.IsChecked = true;
        _updatingPaddleGearBenchUi = false;
        ApplyPaddleGearBenchOptionsFromControls("Local Gear Test Mode enabled from Start Gear Test Listener.");
        await StartPaddleInputListenerForWorkflowAsync("local gear test");
        UpdateLocalGearTestStatus();
    }

    private void MockGearPulseControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingMockGearPulseUi)
        {
            return;
        }

        ApplyMockGearPulseOptionsFromControls("Mock P-HPR gear routing preferences updated.");
    }

    private void MockGearPulseControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingMockGearPulseUi)
        {
            return;
        }

        ApplyMockGearPulseOptionsFromControls("Mock P-HPR gear routing target updated.");
    }

    private void MockGearPulseControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingMockGearPulseUi)
        {
            return;
        }

        ApplyMockGearPulseOptionsFromControls("Mock P-HPR gear pulse profile updated.");
    }

    private void MockPedalEffectsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingMockPedalEffectsUi)
        {
            return;
        }

        ApplyMockPedalEffectsOptionsFromControls("Mock P-HPR pedal effects preferences updated.");
    }

    private void MockPedalEffectsControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingMockPedalEffectsUi)
        {
            return;
        }

        ApplyMockPedalEffectsOptionsFromControls("Mock P-HPR pedal effects target updated.");
    }

    private void MockPedalEffectsControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingMockPedalEffectsUi)
        {
            return;
        }

        ApplyMockPedalEffectsOptionsFromControls("Mock P-HPR pedal effect profile updated.");
    }

    private void ClearMockGearPulseDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        _mockGearPulseRouter.ClearDiagnostics();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Mock P-HPR gear routing diagnostics cleared.";
    }

    private async void MockGearPulseEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _mockGearPulseRouter.EmergencyStopAsync();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdatePhprPedalsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = result.Message;
    }

    private void ClearMockGearPulseEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        _mockGearPulseRouter.ClearEmergencyStop();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdatePhprPedalsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Mock P-HPR emergency stop cleared; no hardware write was performed.";
    }

    private void ClearMockPedalEffectsDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        _mockPedalEffectsRouter.ClearDiagnostics();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Mock P-HPR pedal effects diagnostics cleared.";
    }

    private async void MockPedalEffectsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _mockPedalEffectsRouter.EmergencyStopAsync();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdatePhprPedalsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = result.Message;
    }

    private void ClearMockPedalEffectsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        _mockPedalEffectsRouter.ClearEmergencyStop();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdatePhprPedalsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Mock P-HPR emergency stop cleared; no hardware write was performed.";
    }

    private void RealPhprDirectControlCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingRealPhprDirectControlUi)
        {
            return;
        }

        _updatingRealPhprDirectControlUi = true;
        RealPhprDirectControlArmCheckBox.IsChecked = RealPhprDirectControlEnabledCheckBox.IsChecked == true;
        _updatingRealPhprDirectControlUi = false;

        ConfigureRealPhprOutputFromControls("Real P-HPR direct-control settings updated for this session only.");
    }

    private void RealPhprDirectControlCheckBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingRealPhprDirectControlUi)
        {
            return;
        }

        ConfigureRealPhprOutputFromControls("Real P-HPR direct-control settings updated for this session only.");
    }

    private void RealPhprCandidateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingRealPhprDirectControlUi)
        {
            return;
        }

        ApplySelectedRealPhprCandidateToControls();
        ConfigureRealPhprOutputFromControls("Real P-HPR direct-output candidate selected for this session only.");
    }

    private void RealPhprDirectControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingRealPhprDirectControlUi)
        {
            return;
        }

        ConfigureRealPhprOutputFromControls("Real P-HPR direct-control selection/profile updated for this session only.");
    }

    private async void RefreshRealPhprCandidatesButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRealPhprCandidateItemsAsync(autoSelectPreferred: false);
    }

    private void ApplyRealPhprSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigureRealPhprOutputFromControls("Real P-HPR manual selection applied for this session only.");
    }

    private void AuthorizeRealPhprWritesButton_Click(object sender, RoutedEventArgs e)
    {
        var authorized = _phprWriteAuthorization.TryAuthorize(RealPhprApprovalPhraseTextBox.Text);
        RealPhprApprovalPhraseTextBox.Text = string.Empty;
        UpdatePhprWriteAuthorizationStatus();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = authorized
            ? "Controlled real P-HPR writes authorized for this session."
            : "Controlled real P-HPR write authorization failed.";
    }

    private void DryRunRealPhprSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigureRealPhprOutputFromControls("Real P-HPR direct-output dry run prepared; no HID writer was opened."))
        {
            return;
        }

        var diagnostics = _realPhprOutput.GetDiagnostics();
        var dryRun = PHprDirectOutputDryRunValidator.Validate(
            _realPhprOptions,
            _phprSoftwareCoexistenceSnapshot.Status,
            _outputInterlock.Current,
            diagnostics.Output.IsEmergencyStopActive,
            _phprWriteAuthorization.Current);
        RealPhprCandidatePickerStatusText.Text = dryRun.Issues.Count == 0
            ? $"{dryRun.Summary} Dry-run blockers: none. No HID writer was opened."
            : $"{dryRun.Summary} Dry-run blockers: {string.Join("; ", dryRun.Issues)} No HID writer was opened.";
        UpdateRealPhprDirectControlStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = dryRun.CanPulse
            ? "Real P-HPR dry run passed all gates. No HID writer was opened."
            : "Real P-HPR dry run is blocked. No HID writer was opened.";
    }

    private async void OpenCheckRealPhprSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfigureRealPhprOutputFromControls("Real P-HPR open-check prepared; no output report or feature report will be sent."))
        {
            return;
        }

        var result = await _phprHidOpenCheckRunner.RunAsync(
            _realPhprOptions.Selector,
            _realPhprOptions.CandidateHasOpenableHidPath,
            _realPhprOptions.CandidateIsRawInputOnly,
            allowHardwareAccess: true);
        ApplyRealPhprOpenCheckResult(result);
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        RealPhprCandidatePickerStatusText.Text =
            $"{result.Message} attempted {result.Attempted}; succeeded {result.Succeeded}; failed {result.Failed}; open {result.OpenStatus?.ToString() ?? "none"}; close {result.CloseStatus?.ToString() ?? "none"}; sanitized error {result.SanitizedErrorCategory ?? "none"}. No output report or feature report was sent.";
        FooterStatusText.Text = result.Succeeded
            ? "Real P-HPR HID open-check passed. No output report or feature report was sent."
            : "Real P-HPR HID open-check failed safely. No output report or feature report was sent.";
    }

    private async void TestRealPhprBrakePulseButton_Click(object sender, RoutedEventArgs e)
    {
        await TriggerRealPhprManualPulseAsync(PHprModuleId.Brake);
    }

    private async void TestRealPhprThrottlePulseButton_Click(object sender, RoutedEventArgs e)
    {
        await TriggerRealPhprManualPulseAsync(PHprModuleId.Throttle);
    }

    private async void RealPhprEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigurePhprDirectRuntime();
        await _phprDirectRuntime.EmergencyStopAsync("advanced direct-control emergency stop button");
        RevokePhprWriteAuthorization("Real P-HPR emergency stop requested.");
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePaddleGearBenchStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Real P-HPR emergency stop latched; stop reports were attempted only if a device was selected.";
    }

    private void ClearRealPhprEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigurePhprDirectRuntime();
        _phprDirectRuntime.ClearEmergencyStop();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePaddleGearBenchStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Real P-HPR emergency stop cleared; direct control still requires enable, selected device, and clear readiness checks.";
    }

    private void AdvancedDiagnosticsEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingAdvancedDiagnosticsUi)
        {
            return;
        }

        _advancedDiagnosticsEnabled = AdvancedDiagnosticsEnabledCheckBox.IsChecked == true;
        SaveAppSettings();
        UpdateAdvancedDiagnosticsVisibility();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = _advancedDiagnosticsEnabled
            ? "Advanced diagnostics enabled. Direct-control research controls remain guarded."
            : "Advanced diagnostics hidden. Normal device controls remain available.";
    }

    private void PhprPedalsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingPhprPedalsUi)
        {
            return;
        }

        ApplyPhprPedalsNormalOptionsFromControls("P-HPR pedal settings updated.");
    }

    private void PhprPedalsModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPhprPedalsUi)
        {
            return;
        }

        if (PhprPedalsModeComboBox.SelectedItem is PhprPedalsModeOption { Mode: PhprPedalsMode.Disabled })
        {
            _updatingPhprPedalsUi = true;
            PhprPedalsMasterEnableCheckBox.IsChecked = false;
            _updatingPhprPedalsUi = false;
        }
        else if (PhprPedalsModeComboBox.SelectedItem is PhprPedalsModeOption)
        {
            _updatingPhprPedalsUi = true;
            PhprPedalsMasterEnableCheckBox.IsChecked = true;
            _updatingPhprPedalsUi = false;
        }

        ApplyPhprPedalsNormalOptionsFromControls("P-HPR pedal mode updated.");
    }

    private void PhprPedalsControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPhprPedalsUi)
        {
            return;
        }

        ApplyPhprPedalsNormalOptionsFromControls("P-HPR pedal pulse profile updated.");
    }

    private async void PhprPedalsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        await _mockGearPulseRouter.EmergencyStopAsync();
        await _mockPedalEffectsRouter.EmergencyStopAsync();
        ConfigurePhprDirectRuntime();
        await _phprDirectRuntime.EmergencyStopAsync("normal P-HPR pedals emergency stop button");
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePaddleGearBenchStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "P-HPR emergency stop latched for mock and real outputs.";
    }

    private void ClearPhprPedalsEmergencyStopButton_Click(object sender, RoutedEventArgs e)
    {
        _mockGearPulseRouter.ClearEmergencyStop();
        _mockPedalEffectsRouter.ClearEmergencyStop();
        ConfigurePhprDirectRuntime();
        _phprDirectRuntime.ClearEmergencyStop();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePaddleGearBenchStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "P-HPR emergency stop cleared. Direct output still requires enable, selected device, and clear coexistence.";
    }

    private async void PhprPedalsStopAllClearDeviceStateButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigurePhprDirectRuntime();
        var result = await _phprDirectRuntime.StopAllAsync("manual P-HPR Stop All / Clear Device State button");
        ApplyPaddleGearBenchRuntimeBlockToControls();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePaddleGearBenchStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = result.Succeeded
            ? "P-HPR Stop All / Clear Device State completed; brake and throttle stop reports were sent and the unclean marker is clear."
            : $"P-HPR Stop All / Clear Device State did not prove safe state: {result.Message}";
    }

    private async void TestPhprBrakePulseButton_Click(object sender, RoutedEventArgs e)
    {
        await TriggerNormalPhprTestPulseAsync(PHprModuleId.Brake);
    }

    private async void TestPhprThrottlePulseButton_Click(object sender, RoutedEventArgs e)
    {
        await TriggerNormalPhprTestPulseAsync(PHprModuleId.Throttle);
    }
}

