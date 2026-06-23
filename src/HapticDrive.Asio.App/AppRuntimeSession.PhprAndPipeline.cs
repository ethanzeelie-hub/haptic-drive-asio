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
    private void PhprValidationControl_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
    }

    private void PhprValidationControl_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
    }

    private void RefreshPhprValidationChecklistButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "P-HPR validation checklist refreshed; no hardware output was triggered.";
    }

    private void ExportPhprValidationResultButton_Click(object sender, RoutedEventArgs e)
    {
        var result = BuildPhprManualValidationResult();
        var evaluation = result.Evaluate();
        if (evaluation.IsBlockedPass)
        {
            PhprValidationStatusText.Text = $"Cannot mark P-HPR validation as pass yet: {FormatPhprValidationIssues(evaluation.Issues)}";
            FooterStatusText.Text = "P-HPR validation pass blocked until required manual fields and confirmations are complete.";
            return;
        }

        var directory = GetLocalValidationResultsDirectory();
        _lastPhprValidationExportPath = _phprValidationExporter.ExportMarkdown(result, directory);
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = $"P-HPR validation result exported locally to {_lastPhprValidationExportPath}.";
    }

    private void ApplyShiftIntentOptionsFromControls(string footerMessage)
    {
        _shiftIntentProcessor.Configure(ControlSettingsSnapshotBuilder.BuildShiftIntentOptions(new ShiftIntentControlInputs(
            IsEnabled: ShiftIntentEnabledCheckBox.IsChecked == true,
            SelectedMode: ShiftIntentModeComboBox.SelectedItem is ShiftIntentMode selectedMode ? selectedMode : null)));
        SaveAppSettings();
        UpdateShiftIntentStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
    }

    private void ApplyPaddleGearBenchOptionsFromControls(string footerMessage)
    {
        if (!TryBuildPaddleGearBenchOptionsFromControls(out var options, out var message))
        {
            PaddleGearBenchStatusText.Text = message;
            FooterStatusText.Text = message;
            return;
        }

        _paddleGearBenchTestController.Configure(options);
        _updatingPaddleGearBenchUi = true;
        PaddleGearBenchArmCheckBox.IsChecked = options.IsArmed;
        _updatingPaddleGearBenchUi = false;
        ConfigurePhprDirectRuntime();
        ApplyPaddleGearBenchRuntimeBlockToControls();
        UpdatePaddleGearBenchStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
    }

    private void ApplyPaddleGearBenchRuntimeBlockToControls()
    {
        var runtime = _phprDirectRuntime.GetSnapshot();
        if (!runtime.DisabledAfterUncleanShutdown && !runtime.UncleanShutdownMarkerExists)
        {
            return;
        }

        var current = _paddleGearBenchTestController.GetSnapshot().Options;
        _paddleGearBenchTestController.Configure(current with
        {
            IsEnabled = false,
            IsArmed = false
        });
        _updatingPaddleGearBenchUi = true;
        PaddleGearBenchEnabledCheckBox.IsChecked = false;
        PaddleGearBenchArmCheckBox.IsChecked = false;
        _updatingPaddleGearBenchUi = false;
        UpdatePaddleGearBenchStatus();
        FooterStatusText.Text = "P-HPR bench disabled after previous unclean shutdown. Press P-HPR Stop All / Clear Device State before retesting.";
    }

    private void UpdateLocalGearTestStatus()
    {
        var readiness = EvaluateLocalGearTestReadiness();
        var presentation = LocalGearReadinessPresenter.Build(readiness, _localGearTestAutoStartListener);
        LocalGearTestStatusText.Text = presentation.StatusText;
        StartGearTestListenerButton.IsEnabled = presentation.StartListenerEnabled;
        StartGearTestListenerButton.ToolTip = presentation.StartListenerToolTip;
    }

    private LocalGearTestReadiness EvaluateLocalGearTestReadiness()
    {
        ConfigurePhprDirectRuntime();
        var manualAsio = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        var paddle = _paddleInputSource.GetPaddleSnapshot();
        var runtime = _phprDirectRuntime.GetSnapshot();
        var selectionBlocker = BuildPaddleInputSelectionBlocker();
        var bst1Ready = manualAsio.OutputMode == AudioOutputDeviceKind.Asio.ToString()
            && manualAsio.AsioArmed
            && manualAsio.SelectedOutputChannel is >= 0
            && !manualAsio.EmergencyMute
            && !manualAsio.NormalMute;
        return LocalGearTestReadiness.Evaluate(
            _localGearTestModeEnabled,
            _localGearTestAutoStartListener,
            paddle,
            selectionBlocker,
            _paddleMapping.LeftPaddleButtonId is not null,
            _paddleMapping.RightPaddleButtonId is not null,
            runtime.DirectReady,
            _bst1PaddleGearPulseEnabled,
            bst1Ready);
    }

    private void ApplyMockGearPulseOptionsFromControls(string footerMessage)
    {
        if (!TryBuildMockGearPulseOptionsFromControls(out var options, out var message))
        {
            MockGearPulseStatusText.Text = message;
            FooterStatusText.Text = message;
            return;
        }

        _mockGearPulseRouter.Configure(options);
        SaveAppSettings();
        UpdateMockGearPulseStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
    }

    private void ApplyMockPedalEffectsOptionsFromControls(string footerMessage)
    {
        if (!TryBuildMockPedalEffectsOptionsFromControls(out var options, out var message))
        {
            MockPedalEffectsStatusText.Text = message;
            FooterStatusText.Text = message;
            return;
        }

        _mockPedalEffectsRouter.Configure(options);
        SaveAppSettings();
        UpdateMockPedalEffectsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
    }

    private bool ConfigureRealPhprOutputFromControls(string footerMessage, bool saveSafeSettings = true)
    {
        var previousOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        if (!TryBuildRealPhprOptionsFromControls(out var options, out var message)
            || !TryBuildRealRoadVibrationOptionsFromControls(out var roadOptions, out message)
            || !TryBuildRealSlipLockOptionsFromControls(out var slipLockOptions, out message))
        {
            TestRealPhprBrakePulseButton.IsEnabled = false;
            TestRealPhprThrottlePulseButton.IsEnabled = false;
            RealPhprDirectStatusText.Text = message;
            FooterStatusText.Text = message;
            UpdateDiagnosticsStatus();
            return false;
        }

        _realPhprOptions = options;
        if (HasRealPhprAuthorizationInvalidation(previousOptions, _realPhprOptions))
        {
            RevokePhprWriteAuthorization("Real P-HPR direct-control selection changed.");
        }

        _realRoadVibrationOptions = roadOptions;
        _realSlipLockOptions = slipLockOptions;
        _realPhprHidWriter.Configure(options.Selector);
        _realPhprOutput.Configure(options);
        _realPhprGearPulseRouter.Configure(options);
        _realRoadVibrationRouter.Configure(roadOptions);
        _realSlipLockRouter.Configure(slipLockOptions);
        ConfigurePhprDirectRuntime();
        if (saveSafeSettings)
        {
            SaveAppSettings();
        }

        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
        return true;
    }

    private void RevokePhprWriteAuthorization(string reason)
    {
        _phprWriteAuthorization.Revoke(reason);
        UpdatePhprWriteAuthorizationStatus();
    }

    private void UpdatePhprWriteAuthorizationStatus()
    {
        var authorization = _phprWriteAuthorization.Current;
        RealPhprAuthorizationStatusText.Text = authorization.IsAuthorized
            ? $"Session authorization: authorized at {authorization.AuthorizedAtUtc:O}."
            : $"Session authorization: unauthorized. {authorization.Reason}.";
    }

    private bool HasRealPhprAuthorizationInvalidation(PHprRealOutputOptions previous, PHprRealOutputOptions current)
    {
        var left = previous.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits).Selector.Normalize();
        var right = current.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits).Selector.Normalize();
        return !SelectorsOperationallyMatch(left, right)
            || previous.DirectControlEnabled != current.DirectControlEnabled
            || previous.DirectControlArmed != current.DirectControlArmed;
    }

    private static bool SelectorsOperationallyMatch(PHprHidDeviceSelector left, PHprHidDeviceSelector right)
    {
        return string.Equals(left.DevicePath, right.DevicePath, StringComparison.Ordinal)
            && left.ReportId == right.ReportId
            && left.ReportLength == right.ReportLength
            && left.Transport == right.Transport;
    }

    private void ConfigurePhprDirectRuntime()
    {
        var benchSnapshot = _paddleGearBenchTestController.GetSnapshot();
        var paddleSnapshot = _paddleInputSource.GetPaddleSnapshot();
        var realOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits) with
        {
            GearPulseRetriggerMode = benchSnapshot.IsEnabled
                ? PHprGearPulseRetriggerMode.RetriggerLatestPressWins
                : PHprGearPulseRetriggerMode.Conservative
        };
        _phprDirectRuntime.Configure(new PHprDirectRuntimeEnvironment(
            realOptions,
            _phprSoftwareCoexistenceSnapshot.Status,
            _realRoadVibrationOptions.IsEnabled,
            _realSlipLockOptions.IsEnabled,
            benchSnapshot.IsEnabled,
            benchSnapshot.Options.TargetModule,
            BuildPaddleDeviceSummary(paddleSnapshot),
            paddleSnapshot.DebounceSuppressedCount));
    }

    private static string BuildPaddleDeviceSummary(WheelPaddleInputSnapshot snapshot)
    {
        return snapshot.SelectedDevice is null
            ? "none"
            : $"{snapshot.SelectedDevice.DisplayName}; {snapshot.SelectedDevice.DeviceId}; method {snapshot.SelectedDevice.Method}; buttons {snapshot.SelectedDevice.ButtonCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";
    }

    private async Task TriggerRealPhprManualPulseAsync(PHprModuleId moduleId)
    {
        if (!ConfigureRealPhprOutputFromControls($"Real P-HPR {moduleId} pulse settings applied for this session only."))
        {
            return;
        }

        var settings = moduleId == PHprModuleId.Throttle
            ? _realPhprOptions.ThrottleGearPulse
            : _realPhprOptions.BrakeGearPulse;
        if (!settings.IsEnabled)
        {
            FooterStatusText.Text = $"Real P-HPR {moduleId} manual pulse ignored because that pedal is disabled.";
            UpdateRealPhprDirectControlStatus();
            UpdatePhprValidationStatus();
            return;
        }

        ConfigurePhprDirectRuntime();
        var pulse = await _phprDirectRuntime.SendManualPulseAsync(
            moduleId,
            settings,
            BuildManualRealPhprSafetyContext());
        UpdateRealPhprDirectControlStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = pulse.CommandResult.Message;
    }

    private void PaddleInputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPaddleInputUi)
        {
            return;
        }

        if (PaddleInputDeviceComboBox.SelectedItem is PaddleDeviceListItem item)
        {
            _paddleMapping = _paddleMapping with
            {
                SelectedDeviceId = item.Selection.DeviceId,
                SelectedMethod = item.Selection.Method
            };
            SaveAppSettings();
            _paddleInputSource.RefreshMapping(_paddleMapping);
            UpdatePaddleInputStatus();
        }
    }

    private void PaddleMappingControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPaddleInputUi)
        {
            return;
        }

        if (!TryBuildPaddleMappingFromControls(out var mapping, out var message))
        {
            PaddleInputStatusText.Text = message;
            FooterStatusText.Text = message;
            return;
        }

        _paddleMapping = mapping;
        _paddleInputSource.RefreshMapping(_paddleMapping);
        SaveAppSettings();
        UpdatePaddleInputStatus();
        FooterStatusText.Text = "Paddle input mapping preferences saved.";
    }

    private async Task RebuildHapticPipelineForOutputSelectionAsync(
        long generation,
        string footerMessage,
        CancellationToken cancellationToken = default)
    {
        if (_hapticsStarted || _hapticPipeline.GetSnapshot().IsRunning)
        {
            await _hapticPipeline.StopAsync();
        }

        _hapticsStarted = false;

        var previousPipeline = _hapticPipeline;
        var previousIngressWorker = _telemetryIngressWorker;
        _hapticPipeline = CreatePipelineForSelectedOutput();
        _hapticPipeline.ApplyProfile(_currentProfile);
        _telemetryIngressWorker = CreateTelemetryIngressWorker(_hapticPipeline);
        await _telemetryIngressWorker.StartAsync();
        await previousIngressWorker.DisposeAsync();
        await previousPipeline.DisposeAsync();
        var hydrationMessage = await HydrateSelectedOutputReadinessAsync();

        if (!_runtimeLifecycleCoordinator.ShouldApply(generation))
        {
            return;
        }

        await RefreshAsioReadinessDiagnosticsAsync();
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        UpdateHapticsControlState(pipelineSnapshot);
        UpdateRecordingStatus();
        UpdateOutputStatus(pipelineSnapshot.Output);
        UpdateManualAsioHardwareTestStatus();
        UpdateDeviceStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = hydrationMessage is null
            ? footerMessage
            : $"{footerMessage} {hydrationMessage}";
    }

    private HapticPipelineCoordinator CreatePipelineForSelectedOutput()
    {
        var configuration = BuildSelectedOutputConfiguration();
        var pipeline = new HapticPipelineCoordinator(
            GameTelemetryCatalog.CreateAdapter(_selectedGameId),
            configuration,
            CreateSelectedOutputDevice(),
            profile: _currentProfile,
            forwardingDestinations: CreateForwardingDestinations(),
            outputInterlock: _outputInterlock,
            vehicleStateNormalizer: GameTelemetryCatalog.CreateNormalizer(_selectedGameId));
        pipeline.SetManualAsioHardwareTestFlightRecorder(
            new FileManualAsioHardwareTestFlightRecorder(GetLocalValidationResultsDirectory()));
        return pipeline;
    }

    private async Task<string?> HydrateSelectedOutputReadinessAsync()
    {
        if (_selectedOutputKind != AudioOutputDeviceKind.Asio)
        {
            return null;
        }

        var result = await _hapticPipeline.HydrateOutputReadinessAsync();
        return result.Succeeded
            ? "ASIO readiness hydrated without starting output."
            : $"ASIO readiness hydration failed: {result.Message}";
    }

    private IAudioOutputDevice CreateSelectedOutputDevice()
    {
        return _selectedOutputKind switch
        {
            AudioOutputDeviceKind.Null => new NullAudioOutputDevice(),
            AudioOutputDeviceKind.WasapiDebug => new WasapiDebugOutputDevice(),
            AudioOutputDeviceKind.Asio => new AsioAudioOutputDevice(_asioDriverCatalog),
            _ => new NullAudioOutputDevice()
        };
    }

    private AudioOutputConfiguration BuildSelectedOutputConfiguration()
    {
        return _selectedOutputKind == AudioOutputDeviceKind.Asio
            ? AudioOutputConfiguration.Default with
            {
                RequestedDeviceName = _selectedAsioDriverName,
                SelectedOutputChannel = _selectedAsioOutputChannel,
                IsHardwareArmed = _asioArmed
            }
            : AudioOutputConfiguration.Default;
    }

    private IReadOnlyList<UdpTelemetryForwardingDestination> CreateForwardingDestinations()
    {
        var destinations = new List<UdpTelemetryForwardingDestination>();
        foreach (var setting in _forwardingDestinations)
        {
            if (!string.IsNullOrWhiteSpace(setting.Host)
                && setting.Port is >= 1 and <= 65_535)
            {
                destinations.Add(new UdpTelemetryForwardingDestination(
                    setting.Name,
                    setting.Host,
                    setting.Port,
                    setting.Enabled));
            }
        }

        return destinations;
    }

    private async void AllowLanTelemetryCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _allowLanTelemetry = AllowLanTelemetryCheckBox.IsChecked == true;
        UpdateTelemetryListenerWarning();
        SaveAppSettings();
        await RunSerializedLifecycleOperationAsync(
            (_, cancellationToken) => ReconfigureTelemetryReceiverAsync(
                _allowLanTelemetry
                    ? "LAN telemetry enabled."
                    : "Telemetry listener returned to loopback-only mode.",
                cancellationToken),
            "Telemetry listener reconfiguration failed");
    }

    private async void AllowedRemoteAddressesTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TryParseTelemetryRemoteAddresses(AllowedRemoteAddressesTextBox.Text, out var addresses, out var message))
        {
            TelemetryListenerStatusText.Text = message;
            FooterStatusText.Text = message;
            return;
        }

        _allowedTelemetryRemoteAddresses = addresses;
        AllowedRemoteAddressesTextBox.Text = string.Join(", ", _allowedTelemetryRemoteAddresses);
        UpdateTelemetryListenerWarning();
        SaveAppSettings();
        await RunSerializedLifecycleOperationAsync(
            (_, cancellationToken) => ReconfigureTelemetryReceiverAsync(message, cancellationToken),
            "Telemetry allowlist update failed");
    }

    private async Task ReconfigureTelemetryReceiverAsync(string footerMessage, CancellationToken cancellationToken = default)
    {
        var previousReceiver = _telemetryReceiver;
        previousReceiver.PacketReceived -= TelemetryReceiver_PacketReceived;

        await previousReceiver.StopAsync(cancellationToken);
        await previousReceiver.DisposeAsync();

        try
        {
            _telemetryReceiver = CreateTelemetryReceiver();
            _telemetryReceiver.PacketReceived += TelemetryReceiver_PacketReceived;
            await _telemetryReceiver.StartAsync(cancellationToken);
            _telemetryStartError = null;
            FooterStatusText.Text = footerMessage;
        }
        catch (Exception ex)
        {
            _telemetryStartError = ex.Message;
            FooterStatusText.Text = ex.Message;
        }
        UpdateTelemetryStatus();
        UpdateDiagnosticsStatus();
    }

    private async void SaveForwardingDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildForwardingDestinationSetting(out var setting, out var message))
        {
            ForwardingEditorStatusText.Text = message;
            return;
        }

        if (ForwardingDestinationsListBox.SelectedItem is ForwardingDestinationListItem selected
            && selected.Index >= 0
            && selected.Index < _forwardingDestinations.Count)
        {
            _forwardingDestinations[selected.Index] = setting;
        }
        else
        {
            _forwardingDestinations.Add(setting);
        }

        SaveAppSettings();
        RefreshForwardingDestinationItems();
        await RunSerializedLifecycleOperationAsync(
            (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                generation,
                "UDP forwarding destinations updated; haptics are stopped until started explicitly.",
                cancellationToken),
            "UDP forwarding update failed");
    }

    private async void RemoveForwardingDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        if (ForwardingDestinationsListBox.SelectedItem is not ForwardingDestinationListItem selected
            || selected.Index < 0
            || selected.Index >= _forwardingDestinations.Count)
        {
            ForwardingEditorStatusText.Text = "Select a forwarding destination to remove.";
            return;
        }

        var removed = _forwardingDestinations[selected.Index];
        _forwardingDestinations.RemoveAt(selected.Index);
        SaveAppSettings();
        RefreshForwardingDestinationItems();
        ClearForwardingDestinationEditor();
        await RunSerializedLifecycleOperationAsync(
            (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                generation,
                $"Removed UDP forwarding destination {removed.Name}; haptics are stopped until started explicitly.",
                cancellationToken),
            "UDP forwarding removal failed");
    }

    private void ClearForwardingDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        ClearForwardingDestinationEditor();
        ForwardingEditorStatusText.Text = "Forwarding editor cleared.";
    }

    private void ForwardingDestinationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ForwardingDestinationsListBox.SelectedItem is not ForwardingDestinationListItem item)
        {
            return;
        }

        ForwardingNameTextBox.Text = item.Setting.Name;
        ForwardingHostTextBox.Text = item.Setting.Host;
        ForwardingPortTextBox.Text = item.Setting.Port.ToString();
        ForwardingEnabledCheckBox.IsChecked = item.Setting.Enabled;
        ForwardingEditorStatusText.Text = $"Editing {item.Setting.Name}.";
    }

    private bool TryBuildForwardingDestinationSetting(
        out ForwardingDestinationSetting setting,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildForwardingDestinationSetting(
            new ForwardingDestinationControlInputs(
                NameText: ForwardingNameTextBox.Text,
                HostText: ForwardingHostTextBox.Text,
                PortText: ForwardingPortTextBox.Text,
                Enabled: ForwardingEnabledCheckBox.IsChecked == true),
            out setting,
            out message);
    }

    private void RefreshForwardingDestinationItems()
    {
        _forwardingDestinationItems = _forwardingDestinations
            .Select((setting, index) => new ForwardingDestinationListItem(
                index,
                setting,
                $"{(setting.Enabled ? "On" : "Off")} - {setting.Name} -> {setting.Host}:{setting.Port}"))
            .ToList();
        ForwardingDestinationsListBox.ItemsSource = _forwardingDestinationItems;
        UpdateForwardingEditorStatus();
    }

    private void ClearForwardingDestinationEditor()
    {
        ForwardingDestinationsListBox.SelectedIndex = -1;
        ForwardingNameTextBox.Text = "";
        ForwardingHostTextBox.Text = "127.0.0.1";
        ForwardingPortTextBox.Text = "20779";
        ForwardingEnabledCheckBox.IsChecked = true;
    }

    private void UpdateForwardingEditorStatus()
    {
        if (string.IsNullOrWhiteSpace(ForwardingEditorStatusText.Text))
        {
            ForwardingEditorStatusText.Text = "Use 127.0.0.1 or localhost for local tools; choose a port other than the listener port.";
        }

        UpdateTelemetryUdpPresentation();
    }

    private void UpdatePaddleDeviceSelectionItems()
    {
        var devices = _inputDiscoverySnapshot.HasRun
            ? PaddleInputDeviceSelector.OrderForDisplay(
                _inputDiscoverySnapshot.Devices,
                _paddleMapping.SelectedDeviceId)
            : [];

        _paddleDeviceItems = devices
            .Select(device => new PaddleDeviceListItem(
                device,
                InputDeviceSelection.FromDeviceInfo(device),
                $"{device.DisplayName} - {device.ButtonCount?.ToString() ?? "unknown"} button(s) - {device.CandidateKind}"))
            .ToList();

        _updatingPaddleInputUi = true;
        PaddleInputDeviceComboBox.ItemsSource = _paddleDeviceItems;
        PaddleInputDeviceComboBox.DisplayMemberPath = nameof(PaddleDeviceListItem.DisplayText);

        var selected = SelectPreferredPaddleDeviceItem();
        PaddleInputDeviceComboBox.SelectedItem = selected;
        _updatingPaddleInputUi = false;

        if (selected is null)
        {
            _paddleMapping = _paddleMapping with
            {
                SelectedDeviceId = null
            };
            _paddleInputSource.RefreshMapping(_paddleMapping);
        }
        else if (!string.Equals(selected.Selection.DeviceId, _paddleMapping.SelectedDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            _paddleMapping = _paddleMapping with
            {
                SelectedDeviceId = selected.Selection.DeviceId,
                SelectedMethod = selected.Selection.Method
            };
            _paddleInputSource.RefreshMapping(_paddleMapping);
        }
    }

    private PaddleDeviceListItem? SelectPreferredPaddleDeviceItem()
    {
        var selectedDevice = PaddleInputDeviceSelector.SelectPreferred(
            _paddleDeviceItems.Select(item => item.Device),
            _paddleMapping.SelectedDeviceId);
        if (selectedDevice is null)
        {
            return null;
        }

        return _paddleDeviceItems.FirstOrDefault(item =>
            string.Equals(item.Device.DeviceId, selectedDevice.DeviceId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RefreshRealPhprCandidateItemsAsync(bool autoSelectPreferred)
    {
        try
        {
            var previousSelector = _realPhprOptions.Selector.Normalize();
            var candidates = _phprDirectOutputCandidateProvider.DiscoverCandidates();
            _realPhprCandidateItems = candidates
                .Select((candidate, index) => new PhprDirectOutputCandidateListItem(index, candidate, $"[{index}] {candidate.SafeLabel}"))
                .ToList();

            _updatingRealPhprDirectControlUi = true;
            RealPhprCandidateComboBox.ItemsSource = _realPhprCandidateItems;
            RealPhprCandidateComboBox.DisplayMemberPath = nameof(PhprDirectOutputCandidateListItem.DisplayText);
            PhprDirectAutoReadySelection? autoSelection = null;
            if (autoSelectPreferred)
            {
                autoSelection = StartupReadinessPlanner.BuildStartupPhprAutoReadySelection(
                    candidates,
                    _realPhprOptions);
                _realPhprOptions = autoSelection.Options;
            }

            RealPhprCandidateComboBox.SelectedItem = autoSelection?.Candidate is not null
                ? _realPhprCandidateItems.FirstOrDefault(item =>
                    string.Equals(item.Candidate.DevicePath, autoSelection.Candidate.DevicePath, StringComparison.Ordinal))
                : _realPhprCandidateItems.FirstOrDefault(item =>
                    previousSelector.IsSelected
                    && string.Equals(item.Candidate.DevicePath, previousSelector.DevicePath, StringComparison.Ordinal));
            RealPhprDirectControlEnabledCheckBox.IsChecked = _realPhprOptions.DirectControlEnabled;
            RealPhprDirectControlArmCheckBox.IsChecked = _realPhprOptions.DirectControlEnabled;
            _updatingRealPhprDirectControlUi = false;

            ApplySelectedRealPhprCandidateToControls();
            ConfigureRealPhprOutputFromControls(
                autoSelectPreferred
                    ? "Real P-HPR direct candidates auto-refreshed; no output report or feature report was sent."
                    : "Real P-HPR direct-output candidates refreshed; no HID writer was opened.",
                saveSafeSettings: false);

            if (autoSelection?.HasPreferredCandidate == true)
            {
                await RunAutomaticRealPhprReadinessChecksAsync(autoSelection);
            }

            ApplyPersistedPhprPedalsPreferenceToRuntime(saveSafeSettings: false);
            ApplyPaddleGearBenchSettingsToControls();

            UpdateRealPhprDirectControlStatus();
            UpdatePhprPedalsStatus();
            UpdatePaddleGearBenchStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = autoSelection?.HasPreferredCandidate == true
                ? GetPreferredPhprPedalsMode() == PhprPedalsMode.Direct
                    ? $"Preferred real P-HPR candidate refreshed for saved Direct readiness. {_realPhprCandidateItems.Count:N0} candidate(s) found. Open-check used no output report or feature report."
                    : $"Preferred real P-HPR direct candidate selected for startup no-output readiness only. {_realPhprCandidateItems.Count:N0} candidate(s) found. Open-check used no output report or feature report."
                : $"Real P-HPR direct-output candidates refreshed; {_realPhprCandidateItems.Count:N0} HID candidate(s) found. No HID writer was opened.";
        }
        catch (Exception ex)
        {
            _realPhprCandidateItems = [];
            _updatingRealPhprDirectControlUi = true;
            RealPhprCandidateComboBox.ItemsSource = _realPhprCandidateItems;
            _updatingRealPhprDirectControlUi = false;
            RealPhprCandidatePickerStatusText.Text = $"Candidate refresh failed safely before any write path was opened: {ex.Message}";
            FooterStatusText.Text = "Real P-HPR candidate refresh failed safely.";
        }
    }

    private Task RunAutomaticRealPhprReadinessChecksAsync(PhprDirectAutoReadySelection selection)
    {
        var dryRun = PHprDirectOutputDryRunValidator.Validate(
            _realPhprOptions,
            _phprSoftwareCoexistenceSnapshot.Status,
            _outputInterlock.Current,
            _realPhprOutput.GetDiagnostics().Output.IsEmergencyStopActive,
            _phprWriteAuthorization.Current);
        RealPhprCandidatePickerStatusText.Text =
            $"{selection.Message} Automatic open-check was skipped because open-check is manual hardware access. Dry-run can pulse {dryRun.CanPulse}; blockers {(dryRun.Issues.Count == 0 ? "none" : string.Join("; ", dryRun.Issues))}. No output report or feature report was sent.";
        return Task.CompletedTask;
    }

    private void ApplySelectedRealPhprCandidateToControls()
    {
        if (RealPhprCandidateComboBox.SelectedItem is not PhprDirectOutputCandidateListItem item)
        {
            RealPhprCandidatePickerStatusText.Text = _realPhprCandidateItems.Count == 0
                ? "No direct-output candidates have been refreshed. Use Refresh Candidates; private HID paths remain in memory only."
                : "No direct-output candidate selected. Private HID paths remain in memory only.";
            RealPhprInterfaceTextBox.Text = "none";
            RealPhprReportLengthTextBox.Text = SimHubF1EcRealReportEncoder.PayloadLengthBytes.ToString(CultureInfo.InvariantCulture);
            RealPhprReportTransportComboBox.SelectedItem = PHprHidReportTransport.OutputReport;
            return;
        }

        var previousUpdating = _updatingRealPhprDirectControlUi;
        _updatingRealPhprDirectControlUi = true;
        var selector = item.Candidate.ToSelector(
            ControlSettingsSnapshotBuilder.ParseOptionalReportIdOrNull(RealPhprReportIdTextBox.Text));
        RealPhprInterfaceTextBox.Text = selector.InterfaceName;
        RealPhprReportIdTextBox.Text = selector.ReportId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        RealPhprReportLengthTextBox.Text = selector.ReportLength.ToString(CultureInfo.InvariantCulture);
        RealPhprReportTransportComboBox.SelectedItem = selector.Transport;
        _updatingRealPhprDirectControlUi = previousUpdating;
        var reportShape = PHprHidReportShapeValidator.Validate(item.Candidate, selector);
        RealPhprCandidatePickerStatusText.Text =
            $"Selected [{item.Index}] {item.Candidate.SafeLabel}. Transport {selector.Transport}; report ID {FormatReportId(selector.ReportId)}; report length source: {item.Candidate.SelectedReportLengthSource}; expected first bytes {reportShape.ExpectedFirstBytes ?? "unavailable"}. Report-shape validation {reportShape.Succeeded}; {reportShape.Message} Private HID path is held in memory only.";
    }

    private void ApplyRealPhprOpenCheckResult(PHprHidOpenCheckResult result)
    {
        _realPhprOptions = _realPhprOptions with
        {
            OpenCheckAttempted = result.Attempted,
            OpenCheckSucceeded = result.Succeeded,
            OpenCheckFailed = result.Failed,
            OpenCheckSanitizedErrorCategory = result.SanitizedErrorCategory
        };
        _realPhprOutput.Configure(_realPhprOptions);
        _realPhprGearPulseRouter.Configure(_realPhprOptions);
    }

    private void ApplyPaddleMappingToControls()
    {
        var values = ControlSettingsSnapshotBuilder.BuildPaddleMappingControlValues(_paddleMapping);
        _updatingPaddleInputUi = true;
        LeftPaddleButtonTextBox.Text = values.LeftButtonText;
        RightPaddleButtonTextBox.Text = values.RightButtonText;
        PaddleDebounceTextBox.Text = values.DebounceText;
        _updatingPaddleInputUi = false;
        UpdatePaddleDeviceSelectionItems();
        UpdatePaddleInputStatus();
    }

    private bool TryBuildPaddleMappingFromControls(out WheelPaddleMapping mapping, out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildPaddleMapping(
            new PaddleMappingControlInputs(
                SelectedDeviceId: (PaddleInputDeviceComboBox.SelectedItem as PaddleDeviceListItem)?.Selection.DeviceId,
                FallbackSelectedDeviceId: _paddleMapping.SelectedDeviceId,
                LeftButtonText: LeftPaddleButtonTextBox.Text,
                RightButtonText: RightPaddleButtonTextBox.Text,
                DebounceText: PaddleDebounceTextBox.Text),
            out mapping,
            out message);
    }

    private bool TryBuildMockGearPulseOptionsFromControls(
        out PHprGearPulseRouterOptions options,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildMockGearPulseOptions(
            new MockGearPulseControlInputs(
                IsEnabled: MockGearPulseEnabledCheckBox.IsChecked == true,
                TargetModule: MockGearPulseTargetComboBox.SelectedItem is PHprGearPulseTarget mockTarget ? mockTarget : null,
                StrengthText: MockGearPulseStrengthTextBox.Text,
                FrequencyText: MockGearPulseFrequencyTextBox.Text,
                DurationText: MockGearPulseDurationTextBox.Text),
            _mockGearPulseRouter.GetSnapshot().Options,
            out options,
            out message);
    }

    private bool TryBuildPaddleGearBenchOptionsFromControls(
        out PaddleGearBenchTestOptions options,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildPaddleGearBenchOptions(
            new PaddleGearBenchControlInputs(
                IsEnabled: PaddleGearBenchEnabledCheckBox.IsChecked == true,
                TargetModule: PaddleGearBenchTargetComboBox.SelectedItem is PHprGearPulseTarget benchTarget ? benchTarget : null,
                BrakeSourceSettings: _realPhprOptions.BrakeGearPulse,
                ThrottleSourceSettings: _realPhprOptions.ThrottleGearPulse,
                SharedDurationMs: _sharedPhprGearPulseDurationMs),
            out options,
            out message);
    }

    private bool TryBuildMockPedalEffectsOptionsFromControls(
        out PHprPedalEffectsRouterOptions options,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildMockPedalEffectsOptions(
            new MockPedalEffectsControlInputs(
                IsEnabled: MockPedalEffectsEnabledCheckBox.IsChecked == true,
                RoadVibration: new PhprPedalEffectControlInputs(
                    IsEnabled: RoadPedalEffectEnabledCheckBox.IsChecked == true,
                    TargetModule: RoadPedalEffectTargetComboBox.SelectedItem is PHprGearPulseTarget roadTarget ? roadTarget : null,
                    StrengthText: RoadPedalEffectStrengthTextBox.Text,
                    FrequencyText: RoadPedalEffectFrequencyTextBox.Text,
                    DurationText: RoadPedalEffectDurationTextBox.Text),
                WheelSlip: new PhprPedalEffectControlInputs(
                    IsEnabled: SlipPedalEffectEnabledCheckBox.IsChecked == true,
                    TargetModule: SlipPedalEffectTargetComboBox.SelectedItem is PHprGearPulseTarget slipTarget ? slipTarget : null,
                    StrengthText: SlipPedalEffectStrengthTextBox.Text,
                    FrequencyText: SlipPedalEffectFrequencyTextBox.Text,
                    DurationText: SlipPedalEffectDurationTextBox.Text),
                WheelLock: new PhprPedalEffectControlInputs(
                    IsEnabled: LockPedalEffectEnabledCheckBox.IsChecked == true,
                    TargetModule: LockPedalEffectTargetComboBox.SelectedItem is PHprGearPulseTarget lockTarget ? lockTarget : null,
                    StrengthText: LockPedalEffectStrengthTextBox.Text,
                    FrequencyText: LockPedalEffectFrequencyTextBox.Text,
                    DurationText: LockPedalEffectDurationTextBox.Text)),
            _mockPedalEffectsRouter.GetSnapshot().Options,
            out options,
            out message);
    }

    private bool TryBuildRealPhprOptionsFromControls(
        out PHprRealOutputOptions options,
        out string message)
    {
        options = _realPhprOptions;
        if (!TryApplySharedPhprGearPulseDurationFromControls(out var sharedDurationMs, out message)
            || !TryBuildNormalPhprGearPulseSettings(
                "Brake",
                new NormalPhprGearPulseControlInputs(
                    IsEnabled: NormalPhprBrakeEnabledCheckBox.IsChecked == true,
                    StrengthText: NormalPhprBrakeStrengthTextBox.Text,
                    FrequencyText: NormalPhprBrakeFrequencyTextBox.Text),
                sharedDurationMs,
                out var brake,
                out message)
            || !TryBuildNormalPhprGearPulseSettings(
                "Throttle",
                new NormalPhprGearPulseControlInputs(
                    IsEnabled: NormalPhprThrottleEnabledCheckBox.IsChecked == true,
                    StrengthText: NormalPhprThrottleStrengthTextBox.Text,
                    FrequencyText: NormalPhprThrottleFrequencyTextBox.Text),
                sharedDurationMs,
                out var throttle,
                out message))
        {
            return false;
        }

        return ControlSettingsSnapshotBuilder.TryBuildRealPhprOutputOptions(
            new RealPhprDirectControlInputs(
                DirectControlEnabled: RealPhprDirectControlEnabledCheckBox.IsChecked == true,
                ReportIdText: RealPhprReportIdTextBox.Text,
                ReportLengthText: RealPhprReportLengthTextBox.Text,
                Transport: RealPhprReportTransportComboBox.SelectedItem is PHprHidReportTransport transport ? transport : null,
                InterfaceText: RealPhprInterfaceTextBox.Text,
                SelectedCandidate: (RealPhprCandidateComboBox.SelectedItem as PhprDirectOutputCandidateListItem)?.Candidate),
            _realPhprOptions,
            brake,
            throttle,
            out options,
            out message);
    }

    private bool TryBuildRealRoadVibrationOptionsFromControls(
        out PHprRoadVibrationRouterOptions options,
        out string message)
    {
        options = _realRoadVibrationOptions;
        if (!TryBuildRealRoadVibrationPedalSettings(
                "Brake road",
                new RealRoadVibrationPedalControlInputs(
                    IsEnabled: RealRoadBrakeEnabledCheckBox.IsChecked == true,
                    MinimumStrengthText: RealRoadBrakeMinStrengthTextBox.Text,
                    StrengthText: RealRoadBrakeStrengthTextBox.Text,
                    MinimumFrequencyText: RealRoadBrakeMinFrequencyTextBox.Text,
                    FrequencyText: RealRoadBrakeFrequencyTextBox.Text,
                    DurationText: RealRoadBrakeDurationTextBox.Text),
                out var brake,
                out message)
            || !TryBuildRealRoadVibrationPedalSettings(
                "Throttle road",
                new RealRoadVibrationPedalControlInputs(
                    IsEnabled: RealRoadThrottleEnabledCheckBox.IsChecked == true,
                    MinimumStrengthText: RealRoadThrottleMinStrengthTextBox.Text,
                    StrengthText: RealRoadThrottleStrengthTextBox.Text,
                    MinimumFrequencyText: RealRoadThrottleMinFrequencyTextBox.Text,
                    FrequencyText: RealRoadThrottleFrequencyTextBox.Text,
                    DurationText: RealRoadThrottleDurationTextBox.Text),
                out var throttle,
                out message))
        {
            return false;
        }

        return ControlSettingsSnapshotBuilder.TryBuildRealRoadVibrationOptions(
            RealRoadVibrationEnabledCheckBox.IsChecked == true,
            _realRoadVibrationOptions,
            brake,
            throttle,
            out options,
            out message);
    }

    private bool TryBuildRealSlipLockOptionsFromControls(
        out PHprSlipLockRouterOptions options,
        out string message)
    {
        options = _realSlipLockOptions;
        if (!TryBuildRealSlipLockEffectSettings(
                "Wheel slip",
                PHprPedalEffectKind.WheelSlip,
                new RealSlipLockEffectControlInputs(
                    IsEnabled: RealSlipEnabledCheckBox.IsChecked == true,
                    TargetModule: RealSlipTargetComboBox.SelectedItem is PHprGearPulseTarget realSlipTarget ? realSlipTarget : null,
                    MinimumStrengthText: RealSlipMinStrengthTextBox.Text,
                    StrengthText: RealSlipStrengthTextBox.Text,
                    MinimumFrequencyText: RealSlipMinFrequencyTextBox.Text,
                    FrequencyText: RealSlipFrequencyTextBox.Text,
                    TextureCadenceText: RealSlipCadenceTextBox.Text,
                    DurationText: RealSlipDurationTextBox.Text),
                out var slip,
                out message)
            || !TryBuildRealSlipLockEffectSettings(
                "Wheel lock",
                PHprPedalEffectKind.WheelLock,
                new RealSlipLockEffectControlInputs(
                    IsEnabled: RealLockEnabledCheckBox.IsChecked == true,
                    TargetModule: RealLockTargetComboBox.SelectedItem is PHprGearPulseTarget realLockTarget ? realLockTarget : null,
                    MinimumStrengthText: RealLockMinStrengthTextBox.Text,
                    StrengthText: RealLockStrengthTextBox.Text,
                    MinimumFrequencyText: RealLockMinFrequencyTextBox.Text,
                    FrequencyText: RealLockFrequencyTextBox.Text,
                    TextureCadenceText: RealLockCadenceTextBox.Text,
                    DurationText: RealLockDurationTextBox.Text),
                out var wheelLock,
                out message))
        {
            return false;
        }

        return ControlSettingsSnapshotBuilder.TryBuildRealSlipLockOptions(
            RealSlipEnabledCheckBox.IsChecked == true || RealLockEnabledCheckBox.IsChecked == true,
            _realSlipLockOptions,
            slip,
            wheelLock,
            out options,
            out message);
    }

    private static string FormatReportId(byte? reportId)
    {
        return reportId is null ? "none" : $"0x{reportId.Value:X2} ({reportId.Value.ToString(CultureInfo.InvariantCulture)})";
    }

    private void AssignPaddleFromLastChangedButton(PaddleSide side)
    {
        var snapshot = _paddleInputSource.GetPaddleSnapshot();
        if (snapshot.LastChangedButtonId is null)
        {
            PaddleInputStatusText.Text = "No changed button has been observed yet. Start the listener and press a paddle first.";
            return;
        }

        if (side == PaddleSide.Left)
        {
            LeftPaddleButtonTextBox.Text = snapshot.LastChangedButtonId.Value.ToString();
        }
        else if (side == PaddleSide.Right)
        {
            RightPaddleButtonTextBox.Text = snapshot.LastChangedButtonId.Value.ToString();
        }

        if (TryBuildPaddleMappingFromControls(out var mapping, out var message))
        {
            _paddleMapping = mapping;
            _paddleInputSource.RefreshMapping(_paddleMapping);
            SaveAppSettings();
            UpdatePaddleInputStatus();
            FooterStatusText.Text = $"{side} paddle mapped to button {snapshot.LastChangedButtonId.Value}.";
            return;
        }

        PaddleInputStatusText.Text = message;
    }

    private void ApplyShiftIntentSettingsToControls()
    {
        var snapshot = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        _updatingShiftIntentUi = true;
        ShiftIntentEnabledCheckBox.IsChecked = snapshot.IsEnabled;
        ShiftIntentModeComboBox.SelectedItem = snapshot.Mode;
        _updatingShiftIntentUi = false;
        UpdateShiftIntentStatus();
    }

    private void ApplyPaddleGearBenchSettingsToControls()
    {
        var snapshot = _paddleGearBenchTestController.GetSnapshot();
        var values = ControlSettingsSnapshotBuilder.BuildPaddleGearBenchControlValues(
            snapshot.Options,
            _localGearTestModeEnabled,
            _localGearTestAutoStartListener);
        _updatingPaddleGearBenchUi = true;
        PaddleGearBenchEnabledCheckBox.IsChecked = values.IsEnabled;
        PaddleGearBenchArmCheckBox.IsChecked = values.IsArmed;
        PaddleGearBenchTargetComboBox.SelectedItem = values.TargetModule;
        PaddleGearBenchOutputModeComboBox.SelectedItem = values.OutputMode;
        PaddleGearBenchStrengthTextBox.Text = values.StrengthText;
        PaddleGearBenchFrequencyTextBox.Text = values.FrequencyText;
        PaddleGearBenchDurationTextBox.Text = values.DurationText;
        LocalGearTestModeCheckBox.IsChecked = values.LocalGearTestModeEnabled;
        LocalGearTestAutoStartListenerCheckBox.IsChecked = values.LocalGearTestAutoStartListener;
        _updatingPaddleGearBenchUi = false;
        UpdateLocalGearTestStatus();
        UpdatePaddleGearBenchStatus();
    }

    private void ApplyMockGearPulseSettingsToControls()
    {
        var snapshot = _mockGearPulseRouter.GetSnapshot();
        var values = ControlSettingsSnapshotBuilder.BuildMockGearPulseControlValues(snapshot.Options);
        _updatingMockGearPulseUi = true;
        MockGearPulseEnabledCheckBox.IsChecked = values.IsEnabled;
        MockGearPulseTargetComboBox.SelectedItem = values.TargetModule;
        MockGearPulseStrengthTextBox.Text = values.StrengthText;
        MockGearPulseFrequencyTextBox.Text = values.FrequencyText;
        MockGearPulseDurationTextBox.Text = values.DurationText;
        _updatingMockGearPulseUi = false;
        UpdateMockGearPulseStatus();
    }

    private void ApplyMockPedalEffectsSettingsToControls()
    {
        var snapshot = _mockPedalEffectsRouter.GetSnapshot();
        var values = ControlSettingsSnapshotBuilder.BuildMockPedalEffectsControlValues(snapshot.Options);
        _updatingMockPedalEffectsUi = true;
        MockPedalEffectsEnabledCheckBox.IsChecked = values.IsEnabled;
        RoadPedalEffectEnabledCheckBox.IsChecked = values.RoadVibration.IsEnabled;
        RoadPedalEffectTargetComboBox.SelectedItem = values.RoadVibration.TargetModule;
        RoadPedalEffectStrengthTextBox.Text = values.RoadVibration.StrengthText;
        RoadPedalEffectFrequencyTextBox.Text = values.RoadVibration.FrequencyText;
        RoadPedalEffectDurationTextBox.Text = values.RoadVibration.DurationText;
        SlipPedalEffectEnabledCheckBox.IsChecked = values.WheelSlip.IsEnabled;
        SlipPedalEffectTargetComboBox.SelectedItem = values.WheelSlip.TargetModule;
        SlipPedalEffectStrengthTextBox.Text = values.WheelSlip.StrengthText;
        SlipPedalEffectFrequencyTextBox.Text = values.WheelSlip.FrequencyText;
        SlipPedalEffectDurationTextBox.Text = values.WheelSlip.DurationText;
        LockPedalEffectEnabledCheckBox.IsChecked = values.WheelLock.IsEnabled;
        LockPedalEffectTargetComboBox.SelectedItem = values.WheelLock.TargetModule;
        LockPedalEffectStrengthTextBox.Text = values.WheelLock.StrengthText;
        LockPedalEffectFrequencyTextBox.Text = values.WheelLock.FrequencyText;
        LockPedalEffectDurationTextBox.Text = values.WheelLock.DurationText;
        _updatingMockPedalEffectsUi = false;
        UpdateMockPedalEffectsStatus();
    }

    private void ApplyRealPhprOptionsToControls()
    {
        var values = ControlSettingsSnapshotBuilder.BuildRealPhprControlValues(
            _realPhprOptions,
            _realRoadVibrationOptions,
            _realSlipLockOptions);
        var options = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        _updatingRealPhprDirectControlUi = true;
        RealPhprDirectControlEnabledCheckBox.IsChecked = values.DirectControlEnabled;
        RealPhprDirectControlArmCheckBox.IsChecked = values.DirectControlArmed;
        RealPhprCandidateComboBox.ItemsSource = _realPhprCandidateItems;
        RealPhprCandidateComboBox.DisplayMemberPath = nameof(PhprDirectOutputCandidateListItem.DisplayText);
        RealPhprCandidateComboBox.SelectedItem = _realPhprCandidateItems.FirstOrDefault(item =>
            options.Selector.IsSelected
            && string.Equals(item.Candidate.DevicePath, options.Selector.DevicePath, StringComparison.Ordinal));
        RealPhprInterfaceTextBox.Text = values.InterfaceText;
        RealPhprReportIdTextBox.Text = values.ReportIdText;
        RealPhprReportLengthTextBox.Text = values.ReportLengthText;
        RealPhprReportTransportComboBox.ItemsSource = _realPhprReportTransportOptions;
        RealPhprReportTransportComboBox.SelectedItem = values.ReportTransport;
        RealPhprApprovalPhraseTextBox.Text = string.Empty;
        UpdatePhprWriteAuthorizationStatus();
        RealPhprCandidatePickerStatusText.Text = "Direct-output candidates have not been refreshed. Private HID paths are kept in memory only after refresh.";
        RealPhprBrakeEnabledCheckBox.IsChecked = values.BrakeGearPulse.IsEnabled;
        RealPhprBrakeStrengthTextBox.Text = values.BrakeGearPulse.StrengthText;
        RealPhprBrakeFrequencyTextBox.Text = values.BrakeGearPulse.FrequencyText;
        RealPhprBrakeDurationTextBox.Text = values.BrakeGearPulse.DurationText;
        RealPhprThrottleEnabledCheckBox.IsChecked = values.ThrottleGearPulse.IsEnabled;
        RealPhprThrottleStrengthTextBox.Text = values.ThrottleGearPulse.StrengthText;
        RealPhprThrottleFrequencyTextBox.Text = values.ThrottleGearPulse.FrequencyText;
        RealPhprThrottleDurationTextBox.Text = values.ThrottleGearPulse.DurationText;
        RealRoadVibrationEnabledCheckBox.IsChecked = values.RealRoadVibrationEnabled;
        RealRoadBrakeEnabledCheckBox.IsChecked = values.BrakeRoadVibration.IsEnabled;
        RealRoadBrakeMinStrengthTextBox.Text = values.BrakeRoadVibration.MinimumStrengthText;
        RealRoadBrakeStrengthTextBox.Text = values.BrakeRoadVibration.StrengthText;
        RealRoadBrakeMinFrequencyTextBox.Text = values.BrakeRoadVibration.MinimumFrequencyText;
        RealRoadBrakeFrequencyTextBox.Text = values.BrakeRoadVibration.FrequencyText;
        RealRoadBrakeDurationTextBox.Text = values.BrakeRoadVibration.DurationText;
        RealRoadThrottleEnabledCheckBox.IsChecked = values.ThrottleRoadVibration.IsEnabled;
        RealRoadThrottleMinStrengthTextBox.Text = values.ThrottleRoadVibration.MinimumStrengthText;
        RealRoadThrottleStrengthTextBox.Text = values.ThrottleRoadVibration.StrengthText;
        RealRoadThrottleMinFrequencyTextBox.Text = values.ThrottleRoadVibration.MinimumFrequencyText;
        RealRoadThrottleFrequencyTextBox.Text = values.ThrottleRoadVibration.FrequencyText;
        RealRoadThrottleDurationTextBox.Text = values.ThrottleRoadVibration.DurationText;
        RealSlipEnabledCheckBox.IsChecked = values.WheelSlip.IsEnabled;
        RealSlipTargetComboBox.SelectedItem = values.WheelSlip.TargetModule;
        RealSlipMinStrengthTextBox.Text = values.WheelSlip.MinimumStrengthText;
        RealSlipStrengthTextBox.Text = values.WheelSlip.StrengthText;
        RealSlipMinFrequencyTextBox.Text = values.WheelSlip.MinimumFrequencyText;
        RealSlipFrequencyTextBox.Text = values.WheelSlip.FrequencyText;
        RealSlipCadenceTextBox.Text = values.WheelSlip.TextureCadenceText;
        RealSlipDurationTextBox.Text = values.WheelSlip.DurationText;
        RealLockEnabledCheckBox.IsChecked = values.WheelLock.IsEnabled;
        RealLockTargetComboBox.SelectedItem = values.WheelLock.TargetModule;
        RealLockMinStrengthTextBox.Text = values.WheelLock.MinimumStrengthText;
        RealLockStrengthTextBox.Text = values.WheelLock.StrengthText;
        RealLockMinFrequencyTextBox.Text = values.WheelLock.MinimumFrequencyText;
        RealLockFrequencyTextBox.Text = values.WheelLock.FrequencyText;
        RealLockCadenceTextBox.Text = values.WheelLock.TextureCadenceText;
        RealLockDurationTextBox.Text = values.WheelLock.DurationText;
        _updatingRealPhprDirectControlUi = false;
        ConfigureRealPhprOutputFromControls("Real P-HPR direct control initialized; startup auto-selection runs after load.", saveSafeSettings: false);
    }

    private void ApplyAdvancedDiagnosticsPreferenceToControls()
    {
        _updatingAdvancedDiagnosticsUi = true;
        AdvancedDiagnosticsEnabledCheckBox.IsChecked = _advancedDiagnosticsEnabled;
        _updatingAdvancedDiagnosticsUi = false;
        UpdateAdvancedDiagnosticsVisibility();
    }

    private void UpdateAdvancedDiagnosticsVisibility()
    {
        AdvancedDiagnosticsContentPanel.Visibility = _advancedDiagnosticsEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        AdvancedDiagnosticsGateText.Text = _advancedDiagnosticsEnabled
            ? "Advanced diagnostics are visible. Real direct-control output still requires enable, selected device, clear coexistence, and clear emergency stop."
            : "Advanced diagnostics are hidden by default. Normal setup stays on Devices, and manual testing stays on Testing / Validation.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            SettingsPanel.Visibility = _advancedDiagnosticsEnabled ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = _advancedDiagnosticsEnabled ? Visibility.Visible : Visibility.Collapsed;
            PageStatusText.Text = _advancedDiagnosticsEnabled
                ? "Advanced diagnostics visible; direct hardware output remains guarded."
                : "Advanced diagnostics hidden. Enable the checkbox to show research controls, settings, and full diagnostics.";
        }
    }

    private void ApplyPhprPedalsNormalSettingsToControls()
    {
        var mode = GetPreferredPhprPedalsMode();
        var options = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        _sharedPhprGearPulseDurationMs = Bst1GearPulseDurationSync.ResolveSharedDuration(
            options.BrakeGearPulse,
            options.ThrottleGearPulse);
        var values = ControlSettingsSnapshotBuilder.BuildNormalPhprPedalsControlValues(options, _sharedPhprGearPulseDurationMs);
        _updatingPhprPedalsUi = true;
        PhprPedalsMasterEnableCheckBox.IsChecked = mode != PhprPedalsMode.Disabled;
        PhprPedalsModeComboBox.SelectedItem = _phprPedalsModeOptions.First(option => option.Mode == mode);
        SyncSharedPhprGearPulseDurationControls(values.SharedDurationMs);
        NormalPhprBrakeEnabledCheckBox.IsChecked = values.BrakeGearPulse.IsEnabled;
        NormalPhprBrakeStrengthTextBox.Text = values.BrakeGearPulse.StrengthText;
        NormalPhprBrakeFrequencyTextBox.Text = values.BrakeGearPulse.FrequencyText;
        NormalPhprBrakeDurationTextBox.Text = values.BrakeGearPulse.DurationText;
        NormalPhprThrottleEnabledCheckBox.IsChecked = values.ThrottleGearPulse.IsEnabled;
        NormalPhprThrottleStrengthTextBox.Text = values.ThrottleGearPulse.StrengthText;
        NormalPhprThrottleFrequencyTextBox.Text = values.ThrottleGearPulse.FrequencyText;
        NormalPhprThrottleDurationTextBox.Text = values.ThrottleGearPulse.DurationText;
        _updatingPhprPedalsUi = false;
        UpdatePhprPedalsStatus();
    }

    private bool ApplyPhprPedalsNormalOptionsFromControls(string footerMessage)
    {
        if (!TryApplySharedPhprGearPulseDurationFromControls(out var sharedDurationMs, out var message)
            || !TryBuildNormalPhprGearPulseSettings(
                "Brake",
                new NormalPhprGearPulseControlInputs(
                    IsEnabled: NormalPhprBrakeEnabledCheckBox.IsChecked == true,
                    StrengthText: NormalPhprBrakeStrengthTextBox.Text,
                    FrequencyText: NormalPhprBrakeFrequencyTextBox.Text),
                sharedDurationMs,
                out var brake,
                out message)
            || !TryBuildNormalPhprGearPulseSettings(
                "Throttle",
                new NormalPhprGearPulseControlInputs(
                    IsEnabled: NormalPhprThrottleEnabledCheckBox.IsChecked == true,
                    StrengthText: NormalPhprThrottleStrengthTextBox.Text,
                    FrequencyText: NormalPhprThrottleFrequencyTextBox.Text),
                sharedDurationMs,
                out var throttle,
                out message))
        {
            PhprPedalsStatusText.Text = message;
            FooterStatusText.Text = message;
            UpdatePhprPedalsStatus();
            return false;
        }

        var mode = GetSelectedPhprPedalsMode();
        _phprPedalsEnabledPreference = mode != PhprPedalsMode.Disabled;
        _phprPedalsModePreference = ToPhprPedalsModePreference(mode);
        _realPhprOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits) with
        {
            DirectControlEnabled = mode == PhprPedalsMode.Direct,
            DirectControlArmed = mode == PhprPedalsMode.Direct,
            BrakeGearPulse = brake,
            ThrottleGearPulse = throttle
        };
        if (mode != PhprPedalsMode.Direct)
        {
            RevokePhprWriteAuthorization("Switched away from Direct P-HPR mode.");
        }

        _realPhprOutput.Configure(_realPhprOptions);
        _realPhprGearPulseRouter.Configure(_realPhprOptions);

        var mockGearOptions = _mockGearPulseRouter.GetSnapshot().Options with
        {
            IsEnabled = mode == PhprPedalsMode.Mock
        };
        _mockGearPulseRouter.Configure(mockGearOptions.Normalize());
        var mockPedalOptions = _mockPedalEffectsRouter.GetSnapshot().Options with
        {
            IsEnabled = mode == PhprPedalsMode.Mock
        };
        _mockPedalEffectsRouter.Configure(mockPedalOptions.Normalize());

        _updatingRealPhprDirectControlUi = true;
        RealPhprDirectControlEnabledCheckBox.IsChecked = _realPhprOptions.DirectControlEnabled;
        RealPhprDirectControlArmCheckBox.IsChecked = _realPhprOptions.DirectControlEnabled;
        var directValues = ControlSettingsSnapshotBuilder.BuildRealPhprControlValues(
            _realPhprOptions,
            _realRoadVibrationOptions,
            _realSlipLockOptions);
        RealPhprBrakeEnabledCheckBox.IsChecked = directValues.BrakeGearPulse.IsEnabled;
        RealPhprBrakeStrengthTextBox.Text = directValues.BrakeGearPulse.StrengthText;
        RealPhprBrakeFrequencyTextBox.Text = directValues.BrakeGearPulse.FrequencyText;
        RealPhprBrakeDurationTextBox.Text = directValues.BrakeGearPulse.DurationText;
        RealPhprThrottleEnabledCheckBox.IsChecked = directValues.ThrottleGearPulse.IsEnabled;
        RealPhprThrottleStrengthTextBox.Text = directValues.ThrottleGearPulse.StrengthText;
        RealPhprThrottleFrequencyTextBox.Text = directValues.ThrottleGearPulse.FrequencyText;
        RealPhprThrottleDurationTextBox.Text = directValues.ThrottleGearPulse.DurationText;
        _updatingRealPhprDirectControlUi = false;

        SaveAppSettings();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
        return true;
    }

    private bool TryApplySharedPhprGearPulseDurationFromControls(out int durationMs, out string message)
    {
        if (!ControlSettingsSnapshotBuilder.TryBuildSharedPhprGearPulseDuration(
                NormalPhprGearDurationTextBox.Text,
                out durationMs,
                out message))
        {
            return false;
        }

        _sharedPhprGearPulseDurationMs = durationMs;
        SyncSharedPhprGearPulseDurationControls(durationMs);
        message = "Shared P-HPR gear pulse duration ready.";
        return true;
    }

    private void SyncSharedPhprGearPulseDurationControls(int durationMs)
    {
        var text = Bst1GearPulseDurationSync.NormalizeGearDuration(durationMs).ToString(CultureInfo.InvariantCulture);
        NormalPhprGearDurationTextBox.Text = text;
        NormalPhprBrakeDurationTextBox.Text = text;
        NormalPhprThrottleDurationTextBox.Text = text;
        PaddleGearBenchDurationTextBox.Text = text;
        if (_bst1PaddleGearSyncDuration)
        {
            Bst1PaddleGearDurationTextBox.Text = text;
        }

        if (Bst1PaddleGearEffectiveDurationText is not null)
        {
            Bst1PaddleGearEffectiveDurationText.Text =
                $"Effective duration: {GetEffectiveBst1PaddleGearDurationMs()} ms ({(_bst1PaddleGearSyncDuration ? "sync" : "custom")}); P-HPR gear {_sharedPhprGearPulseDurationMs} ms; custom BST-1 {_bst1PaddleGearCustomDurationMs} ms.";
        }
    }

    private static bool TryBuildNormalPhprGearPulseSettings(
        string label,
        NormalPhprGearPulseControlInputs inputs,
        int durationMs,
        out PHprRealGearPulseSettings settings,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildNormalPhprGearPulseSettings(
            label,
            inputs,
            durationMs,
            out settings,
            out message);
    }

    private static bool TryBuildRealRoadVibrationPedalSettings(
        string label,
        RealRoadVibrationPedalControlInputs inputs,
        out PHprRoadVibrationPedalSettings settings,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildRealRoadVibrationPedalSettings(
            label,
            inputs,
            out settings,
            out message);
    }

    private static bool TryBuildRealSlipLockEffectSettings(
        string label,
        PHprPedalEffectKind kind,
        RealSlipLockEffectControlInputs inputs,
        out PHprSlipLockEffectSettings settings,
        out string message)
    {
        return ControlSettingsSnapshotBuilder.TryBuildRealSlipLockEffectSettings(
            label,
            kind,
            inputs,
            out settings,
            out message);
    }

    private PhprPedalsMode GetSelectedPhprPedalsMode()
    {
        if (PhprPedalsMasterEnableCheckBox.IsChecked != true)
        {
            return PhprPedalsMode.Disabled;
        }

        return PhprPedalsModeComboBox.SelectedItem is PhprPedalsModeOption option
            ? option.Mode
            : PhprPedalsMode.Mock;
    }

    private PhprPedalsMode GetPreferredPhprPedalsMode()
    {
        if (!_phprPedalsEnabledPreference)
        {
            return PhprPedalsMode.Disabled;
        }

        return FromPhprPedalsModePreference(_phprPedalsModePreference);
    }

    private void ApplyPersistedPhprPedalsPreferenceToRuntime(bool saveSafeSettings, bool updateUi = true)
    {
        var mode = GetPreferredPhprPedalsMode();
        _realPhprOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits) with
        {
            DirectControlEnabled = mode == PhprPedalsMode.Direct,
            DirectControlArmed = mode == PhprPedalsMode.Direct
        };
        if (mode != PhprPedalsMode.Direct)
        {
            RevokePhprWriteAuthorization("Switched away from Direct P-HPR mode.");
        }

        _realPhprOutput.Configure(_realPhprOptions);
        _realPhprGearPulseRouter.Configure(_realPhprOptions);

        var mockGearOptions = _mockGearPulseRouter.GetSnapshot().Options with
        {
            IsEnabled = mode == PhprPedalsMode.Mock
        };
        _mockGearPulseRouter.Configure(mockGearOptions.Normalize());

        var mockPedalOptions = _mockPedalEffectsRouter.GetSnapshot().Options with
        {
            IsEnabled = mode == PhprPedalsMode.Mock
        };
        _mockPedalEffectsRouter.Configure(mockPedalOptions.Normalize());

        ConfigurePhprDirectRuntime();

        if (updateUi && RealPhprDirectControlEnabledCheckBox is not null)
        {
            _updatingRealPhprDirectControlUi = true;
            RealPhprDirectControlEnabledCheckBox.IsChecked = _realPhprOptions.DirectControlEnabled;
            RealPhprDirectControlArmCheckBox.IsChecked = _realPhprOptions.DirectControlEnabled;
            _updatingRealPhprDirectControlUi = false;
        }

        if (updateUi && PhprPedalsMasterEnableCheckBox is not null)
        {
            ApplyPhprPedalsNormalSettingsToControls();
        }

        if (saveSafeSettings)
        {
            SaveAppSettings();
        }
    }

    private static PhprPedalsModePreference ToPhprPedalsModePreference(PhprPedalsMode mode)
    {
        return mode switch
        {
            PhprPedalsMode.Direct => PhprPedalsModePreference.Direct,
            PhprPedalsMode.Disabled => PhprPedalsModePreference.Disabled,
            _ => PhprPedalsModePreference.Mock
        };
    }

    private static DashboardPhprMode ToDashboardPhprMode(PhprPedalsMode mode)
    {
        return mode switch
        {
            PhprPedalsMode.Direct => DashboardPhprMode.Direct,
            PhprPedalsMode.Disabled => DashboardPhprMode.Disabled,
            _ => DashboardPhprMode.Mock
        };
    }

    private static PhprPedalsMode FromPhprPedalsModePreference(PhprPedalsModePreference mode)
    {
        return mode switch
        {
            PhprPedalsModePreference.Direct => PhprPedalsMode.Direct,
            PhprPedalsModePreference.Disabled => PhprPedalsMode.Disabled,
            _ => PhprPedalsMode.Mock
        };
    }

    private async Task TriggerNormalPhprTestPulseAsync(PHprModuleId moduleId)
    {
        if (!ApplyPhprPedalsNormalOptionsFromControls($"P-HPR {moduleId} pulse settings applied."))
        {
            return;
        }

        var mode = GetSelectedPhprPedalsMode();
        if (mode == PhprPedalsMode.Disabled)
        {
            _lastPhprPedalsPulseMessage = "Blocked: enable P-HPR Pedals before sending a test pulse.";
            UpdatePhprPedalsStatus();
            FooterStatusText.Text = _lastPhprPedalsPulseMessage;
            return;
        }

        var settings = moduleId == PHprModuleId.Throttle
            ? _realPhprOptions.ThrottleGearPulse
            : _realPhprOptions.BrakeGearPulse;
        if (!settings.IsEnabled)
        {
            _lastPhprPedalsPulseMessage = $"Blocked: {moduleId} P-HPR pulse is disabled.";
            UpdatePhprPedalsStatus();
            FooterStatusText.Text = _lastPhprPedalsPulseMessage;
            return;
        }

        if (mode == PhprPedalsMode.Mock)
        {
            _mockPhprSafetyOutput.SetSafetyContext(PHprSafetyContext.DefaultMock);
            var command = PHprCommand.Create(
                moduleId,
                settings.Strength01,
                settings.FrequencyHz,
                settings.DurationMs,
                PHprCommandSource.TestBench,
                priority: 100,
                safetyFlags: PHprSafetyFlags.MockOnly);
            var result = await _mockPhprSafetyOutput.SendAsync(command);
            _lastPhprPedalsPulseMessage = result.Status == PHprCommandStatus.Accepted
                ? $"Sent mock {moduleId.ToString().ToLowerInvariant()} pulse: {PhprUiValueConverter.FormatPercent(settings.Strength01)}%, {settings.FrequencyHz:0.###} Hz, {settings.DurationMs} ms."
                : $"Blocked: {result.Message}";
            UpdateMockGearPulseStatus();
            UpdateMockPedalEffectsStatus();
            UpdatePhprPedalsStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = _lastPhprPedalsPulseMessage;
            return;
        }

        ConfigurePhprDirectRuntime();
        var directPulse = await _phprDirectRuntime.SendManualPulseAsync(
            moduleId,
            settings,
            BuildManualRealPhprSafetyContext());
        _lastPhprPedalsPulseMessage = directPulse.Succeeded
            ? $"Sent direct {moduleId.ToString().ToLowerInvariant()} pulse."
            : $"Blocked: {directPulse.CommandResult.Message}";
        UpdateRealPhprDirectControlStatus();
        UpdatePhprPedalsStatus();
        UpdatePhprValidationStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = _lastPhprPedalsPulseMessage;
    }

    private bool TryGetDirectPhprPulseReady(PHprModuleId moduleId, out string message)
    {
        var diagnostics = _realPhprOutput.GetDiagnostics();
        if (!_realPhprOptions.DirectControlEnabled)
        {
            message = "direct control is disabled";
            return false;
        }

        if (!_realPhprOptions.DirectControlArmed)
        {
            message = "direct control is not armed";
            return false;
        }

        if (!_realPhprOptions.Selector.IsSelected)
        {
            message = "no P-HPR device/interface/report is selected";
            return false;
        }

        if (_realPhprOptions.CandidateIsRawInputOnly || !_realPhprOptions.CandidateHasOpenableHidPath)
        {
            message = "selected candidate is Raw Input metadata only or has no openable HID device-interface path";
            return false;
        }

        if (!_realPhprOptions.OpenCheckSucceeded)
        {
            message = "selected candidate has not passed HID open-check";
            return false;
        }

        if (!_realPhprOptions.AllowsDirectPulseReportShape)
        {
            message = _realPhprOptions.ReportShapeValidationFailed
                ? $"selected candidate report shape is blocked: {_realPhprOptions.ReportShapeValidationMessage ?? "validation failed"}"
                : "selected candidate report transport/capability/shape is unavailable";
            return false;
        }

        if (_phprSoftwareCoexistenceSnapshot.Status != PHprSoftwareConflictStatus.Clear)
        {
            message = $"software coexistence is {_phprSoftwareCoexistenceSnapshot.Status}";
            return false;
        }

        if (!_outputInterlock.Current.AllowsOutput)
        {
            message = "global output interlock is latched";
            return false;
        }

        if (diagnostics.Output.IsEmergencyStopActive)
        {
            message = "emergency stop is active";
            return false;
        }

        if (!_phprWriteAuthorization.Current.IsAuthorized)
        {
            message = "session authorization is required";
            return false;
        }

        var settings = moduleId == PHprModuleId.Throttle
            ? _realPhprOptions.ThrottleGearPulse
            : _realPhprOptions.BrakeGearPulse;
        if (!settings.IsEnabled)
        {
            message = $"{moduleId} pulse is disabled";
            return false;
        }

        message = "direct control ready";
        return true;
    }

    private void UpdatePhprPedalsStatus()
    {
        var mode = GetSelectedPhprPedalsMode();
        var mockSnapshot = _mockPhprSafetyOutput.GetSnapshot();
        var realDiagnostics = _realPhprOutput.GetDiagnostics();
        var directReady = TryGetDirectPhprPulseReady(PHprModuleId.Brake, out var directMessage)
            || TryGetDirectPhprPulseReady(PHprModuleId.Throttle, out directMessage);
        var brakeEnabled = _realPhprOptions.BrakeGearPulse.IsEnabled;
        var throttleEnabled = _realPhprOptions.ThrottleGearPulse.IsEnabled;
        var brakeCanPulse = mode == PhprPedalsMode.Mock && brakeEnabled && !mockSnapshot.IsEmergencyStopActive
            || mode == PhprPedalsMode.Direct && brakeEnabled && TryGetDirectPhprPulseReady(PHprModuleId.Brake, out _);
        var throttleCanPulse = mode == PhprPedalsMode.Mock && throttleEnabled && !mockSnapshot.IsEmergencyStopActive
            || mode == PhprPedalsMode.Direct && throttleEnabled && TryGetDirectPhprPulseReady(PHprModuleId.Throttle, out _);

        TestPhprBrakePulseButton.IsEnabled = brakeCanPulse;
        TestPhprThrottlePulseButton.IsEnabled = throttleCanPulse;
        TestPhprBrakePulseButton.ToolTip = BuildNormalPulseToolTip(PHprModuleId.Brake, mode, brakeEnabled, mockSnapshot.IsEmergencyStopActive);
        TestPhprThrottlePulseButton.ToolTip = BuildNormalPulseToolTip(PHprModuleId.Throttle, mode, throttleEnabled, mockSnapshot.IsEmergencyStopActive);

        PhprPedalsModeBadgeText.Text = mode switch
        {
            PhprPedalsMode.Disabled => "Disabled",
            PhprPedalsMode.Mock => mockSnapshot.IsEmergencyStopActive ? "Mock stopped" : "Mock ready",
            PhprPedalsMode.Direct => directReady ? "Direct ready" : "Direct not ready",
            _ => "Unknown"
        };
        PhprPedalsStatusText.Text = mode switch
        {
            PhprPedalsMode.Disabled => "P-HPR pedals disabled. Emergency stop remains available.",
            PhprPedalsMode.Mock => mockSnapshot.IsEmergencyStopActive
                ? "Mock P-HPR emergency stop is active; clear it before mock test pulses."
                : "Mock P-HPR pedals are ready for software-only test pulses.",
            PhprPedalsMode.Direct => directReady
                ? "Direct P-HPR mode is ready for this session."
                : $"Direct P-HPR mode is selected but blocked: {directMessage}.",
            _ => "P-HPR pedal mode unavailable."
        };
        PhprPedalsDeviceStatusText.Text =
            $"Mock output {(mockSnapshot.IsEmergencyStopActive ? "stopped" : "ready")}; direct connection {realDiagnostics.Connection.State}; device {(realDiagnostics.Options.Selector.IsSelected ? "selected" : "not selected")}; direct checks {(realDiagnostics.Options.OpenCheckSucceeded && realDiagnostics.Options.ReportShapeValidationSucceeded ? "ready" : "still blocked")}; authorization {_phprWriteAuthorization.Current.IsAuthorized}; interlock {FormatOnOff(_outputInterlock.Current.AllowsOutput)}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}; emergency stop {FormatOnOff(realDiagnostics.Output.IsEmergencyStopActive)}.";
        PhprPedalsLastResultText.Text = _lastPhprPedalsPulseMessage;
        UpdateDashboardStatus();
    }

    private string BuildNormalPulseToolTip(
        PHprModuleId moduleId,
        PhprPedalsMode mode,
        bool moduleEnabled,
        bool mockEmergencyStopActive)
    {
        if (mode == PhprPedalsMode.Disabled)
        {
            return "Enable P-HPR Pedals first.";
        }

        if (!moduleEnabled)
        {
            return $"{moduleId} pulse is disabled.";
        }

        if (mode == PhprPedalsMode.Mock)
        {
            return mockEmergencyStopActive
                ? "Clear P-HPR emergency stop before sending mock pulses."
                : "Send a safety-limited mock pulse without requiring haptics to be running.";
        }

        return TryGetDirectPhprPulseReady(moduleId, out var message)
            ? "Send a manually gated direct P-HPR pulse."
            : $"Direct mode blocked: {message}.";
    }

    private void SaveAppSettings()
    {
        try
        {
            _settingsStore.Save(AppSettingsSnapshotBuilder.BuildAppSettings(BuildCurrentAppSettingsSaveInputs()));
            _settingsError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _settingsError = $"App settings could not be saved: {ex.Message}";
        }

        UpdateProfileStatus();
    }

    private AppSettingsSaveInputs BuildCurrentAppSettingsSaveInputs()
    {
        var shiftIntent = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var mockGear = _mockGearPulseRouter.GetSnapshot();
        var mockPedalEffects = _mockPedalEffectsRouter.GetSnapshot();
        return new AppSettingsSaveInputs(
            UseLightTheme: _lightTheme,
            AdvancedDiagnosticsEnabled: _advancedDiagnosticsEnabled,
            SelectedGameId: _selectedGameId,
            AllowLanTelemetry: _allowLanTelemetry,
            AllowedTelemetryRemoteAddresses: _allowedTelemetryRemoteAddresses.ToList(),
            SelectedOutputKind: _selectedOutputKind,
            PhprPedalsEnabledPreference: _phprPedalsEnabledPreference,
            PhprPedalsModePreference: _phprPedalsModePreference,
            SelectedAsioDriverName: _selectedAsioDriverName,
            SelectedAsioOutputChannel: _selectedAsioOutputChannel,
            ArmAsioPreference: _asioArmed,
            ReplayTimingPreference: GetSelectedReplayTimingPreference(),
            ForwardingDestinations: _forwardingDestinations.ToList(),
            PaddleMapping: _paddleMapping,
            Bst1PaddleGearPulseEnabled: _bst1PaddleGearPulseEnabled,
            Bst1PaddleGearStrengthPercent: _bst1PaddleGearStrengthPercent,
            Bst1PaddleGearFrequencyHz: _bst1PaddleGearFrequencyHz,
            Bst1PaddleGearUseSharedDuration: _bst1PaddleGearSyncDuration,
            Bst1PaddleGearCustomDurationMs: _bst1PaddleGearCustomDurationMs,
            ShiftIntentEnabled: shiftIntent.IsEnabled,
            ShiftIntentMode: shiftIntent.Mode,
            MockGearPulseRouterOptions: mockGear.Options,
            MockPedalEffectsRouterOptions: mockPedalEffects.Options,
            RealPhprOutputOptions: _realPhprOptions,
            RealRoadVibrationRouterOptions: _realRoadVibrationOptions,
            RealSlipLockRouterOptions: _realSlipLockOptions);
    }

    private HapticProfileSaveResult PersistCurrentAudioProfile()
    {
        var result = _profileStore.SaveAsync(_currentProfile, HapticProfileStore.GetDefaultProfilePath())
            .AsTask()
            .GetAwaiter()
            .GetResult();
        if (result.Succeeded)
        {
            _startupProfileStatusMessage = result.Message;
            _startupProfileValidationMessages = result.ValidationMessages;
        }

        return result;
    }
}

