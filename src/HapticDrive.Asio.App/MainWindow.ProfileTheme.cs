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
    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _lightTheme = !_lightTheme;
        ApplyTheme(_lightTheme);
        SaveAppSettings();
    }

    private void ApplyTheme(bool lightTheme)
    {
        _lightTheme = lightTheme;
        var palette = lightTheme
            ? new ThemePalette(
                "#F3F5F8",
                "#FFFFFF",
                "#F8FAFC",
                "#FFFFFF",
                "#FFFFFF",
                "#E9EEF4",
                "#DFE6F0",
                "#CBD5E1",
                "#94A3B8",
                "#17212B",
                "#5F6C7B",
                "#D92543",
                "#EF4660",
                "#A6162D",
                "#F8DDE3",
                "#C23B35",
                "#E5534B",
                "#9B2C27",
                "#1D8A60",
                "#C27A12",
                "#187CA8",
                "#FFFFFF",
                "#E9EEF4",
                "#FFFFFF")
            : new ThemePalette(
                "#08090D",
                "#0D0F14",
                "#0A0B0F",
                "#0F1218",
                "#121722",
                "#1A202C",
                "#202838",
                "#2A3342",
                "#394456",
                "#F4F7FB",
                "#9AA7B7",
                "#E9364F",
                "#FF5368",
                "#BC2038",
                "#3A121B",
                "#F04438",
                "#FF5F55",
                "#B42318",
                "#35C987",
                "#F2B84B",
                "#4EA8F7",
                "#10151E",
                "#232D3D",
                "#090B10");

        Resources["AppBackgroundBrush"] = BrushFrom(palette.Background);
        Resources["AppChromeBrush"] = BrushFrom(palette.Chrome);
        Resources["AppSidebarBrush"] = BrushFrom(palette.Sidebar);
        Resources["AppTopBarBrush"] = BrushFrom(palette.TopBar);
        Resources["AppSurfaceBrush"] = BrushFrom(palette.Surface);
        Resources["AppSurfaceAltBrush"] = BrushFrom(palette.SurfaceAlt);
        Resources["AppSurfaceRaisedBrush"] = BrushFrom(palette.SurfaceRaised);
        Resources["AppBorderBrush"] = BrushFrom(palette.Border);
        Resources["AppBorderStrongBrush"] = BrushFrom(palette.BorderStrong);
        Resources["AppTextBrush"] = BrushFrom(palette.Text);
        Resources["AppMutedTextBrush"] = BrushFrom(palette.MutedText);
        Resources["AppAccentBrush"] = BrushFrom(palette.Accent);
        Resources["AppAccentHoverBrush"] = BrushFrom(palette.AccentHover);
        Resources["AppAccentPressedBrush"] = BrushFrom(palette.AccentPressed);
        Resources["AppAccentSoftBrush"] = BrushFrom(palette.AccentSoft);
        Resources["AppDangerBrush"] = BrushFrom(palette.Danger);
        Resources["AppDangerHoverBrush"] = BrushFrom(palette.DangerHover);
        Resources["AppDangerPressedBrush"] = BrushFrom(palette.DangerPressed);
        Resources["AppSuccessBrush"] = BrushFrom(palette.Success);
        Resources["AppWarningBrush"] = BrushFrom(palette.Warning);
        Resources["AppInfoBrush"] = BrushFrom(palette.Info);
        Resources["AppInputBrush"] = BrushFrom(palette.Input);
        Resources["AppInputFocusBrush"] = BrushFrom(palette.InputFocus);
        Resources["AppOverlayBrush"] = BrushFrom(palette.Overlay);
        ThemeButton.Content = lightTheme ? "Theme: Light" : "Theme: Dark";

        _updatingSettingsUi = true;
        SettingsLightThemeCheckBox.IsChecked = lightTheme;
        _updatingSettingsUi = false;
    }

    private void TuningControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingTuningUi)
        {
            return;
        }

        var profile = BuildProfileFromControls();
        _profileTuningController.ApplyLiveTuning(profile, _hapticsStarted);
        UpdateMixerStatus();
        UpdateEffectStatus();
        UpdateDiagnosticsStatus();
    }

    private void ThemeSettingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingSettingsUi)
        {
            return;
        }

        ApplyTheme(SettingsLightThemeCheckBox.IsChecked == true);
        SaveAppSettings();
        UpdateProfileStatus();
    }

    private async void ProfileNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingTuningUi)
        {
            return;
        }

        _currentProfile = BuildProfileFromControls();
        await _profileTuningController.CommitProfileNameAsync(_currentProfile);
    }

    private AudioProfileControlInputs BuildCurrentAudioProfileControlInputs()
    {
        return AudioProfileViewSyncCoordinator.BuildCurrentControlInputs(
            ProfilesViewControl,
            EffectsViewControl,
            RoutingMixerViewControl);
    }

    private HapticDriveProfile BuildProfileFromControls()
    {
        return AudioProfileControlSnapshotBuilder.BuildProfile(
            _currentProfile,
            BuildCurrentAudioProfileControlInputs());
    }

    private void ApplyProfileToRuntime(HapticDriveProfile profile)
    {
        var validation = HapticProfileValidator.Validate(profile);
        _currentProfile = validation.Profile;
        _hapticPipeline.ApplyProfile(_currentProfile);
        _testBench.MasterGain = _currentProfile.Mixer.MasterGain;
        _testBench.IsMuted = _currentProfile.Mixer.IsMuted;
        _testBench.SafetyOptions = _currentProfile.ToSafetyOptions(_emergencyMuted);
        _testBench.EmergencyMute = _emergencyMuted;
        PublishRuntimeControlSnapshot();
    }

    private void ApplyProfileToControls(HapticDriveProfile profile)
    {
        var plan = AudioProfileControlSnapshotBuilder.BuildApplicationPlan(profile);
        _updatingTuningUi = true;

        AudioProfileViewSyncCoordinator.ApplyControlValues(
            plan.ControlValues,
            ProfilesViewControl,
            EffectsViewControl,
            RoutingMixerViewControl);

        _updatingTuningUi = false;
        ApplyProfileControlText(plan.TextValues);
        _profileTuningController?.RefreshEffectSettings(plan.SafeProfile);
    }

    private void UpdateProfileControlText(HapticDriveProfile profile)
    {
        ApplyProfileControlText(AudioProfileControlSnapshotBuilder.BuildApplicationPlan(profile).TextValues);
    }

    private void ApplyProfileControlText(AudioProfileControlTextValues values)
    {
        AudioProfileViewSyncCoordinator.ApplyControlText(
            values,
            EffectsViewControl,
            RoutingMixerViewControl);
    }

    private void PublishProfileTuningFeedback(AudioProfileWorkflowFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        if (feedback.ShouldUpdateProfileStatus)
        {
            UpdateProfileStatus(feedback.ProfileStatusMessage, feedback.ProfileValidationMessages);
        }

        if (feedback.FooterStatusText is not null)
        {
            FooterStatusText.Text = feedback.FooterStatusText;
        }
    }

    private PhprEffectProfile BuildPhprEffectProfileFromCurrentSettings(string name)
    {
        return PhprEffectProfile.FromAppSettings(
            string.IsNullOrWhiteSpace(name) ? _currentProfile.Name : name,
            AppSettingsSnapshotBuilder.BuildAppSettings(BuildCurrentAppSettingsSaveInputs()));
    }

    private void ApplyPhprEffectProfileToRuntime(PhprEffectProfile profile)
    {
        var validation = PhprEffectProfileValidator.Validate(profile);
        var settings = validation.Profile.ApplyTo(AppSettings.Default);

        _shiftIntentProcessor.Configure(AppSettingsSnapshotBuilder.CreateShiftIntentOptions(settings.ShiftIntent));
        _mockGearPulseRouter.Configure(AppSettingsSnapshotBuilder.CreateMockGearPulseRouterOptions(settings.MockGearPulseRouting));
        _mockPedalEffectsRouter.Configure(AppSettingsSnapshotBuilder.CreateMockPedalEffectsRouterOptions(settings.MockPedalEffectsRouting));

        var realProfileOptions = AppSettingsSnapshotBuilder.CreateRealPhprOutputOptions(settings.RealPhprGearPulseRouting);
        _realPhprOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits) with
        {
            BrakeGearPulse = realProfileOptions.BrakeGearPulse,
            ThrottleGearPulse = realProfileOptions.ThrottleGearPulse
        };
        _realRoadVibrationOptions = AppSettingsSnapshotBuilder.CreateRealRoadVibrationRouterOptions(settings.RealPhprRoadVibrationRouting);
        _realSlipLockOptions = AppSettingsSnapshotBuilder.CreateRealSlipLockRouterOptions(settings.RealPhprSlipLockRouting);

        _realPhprOutput.Configure(_realPhprOptions);
        _realPhprGearPulseRouter.Configure(_realPhprOptions);
        _realRoadVibrationRouter.Configure(_realRoadVibrationOptions);
        _realSlipLockRouter.Configure(_realSlipLockOptions);

        ApplyShiftIntentSettingsToControls();
        ApplyMockGearPulseSettingsToControls();
        ApplyMockPedalEffectsSettingsToControls();
        PublishRuntimeControlSnapshot();
        ApplyRealPhprOptionsToControls();
        UpdatePhprWorkflowStatus();
        UpdateDeviceStatus();
        UpdateDiagnosticsStatus();
    }

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = BuildProfileFromControls();
        var phprProfile = BuildPhprEffectProfileFromCurrentSettings(profile.Name);
        var result = await _profileStore.SaveAsync(profile, HapticProfileStore.GetDefaultProfilePath());
        var phprResult = await _phprProfileStore.SaveAsync(phprProfile, PhprEffectProfileStore.GetDefaultProfilePath());

        if (result.Succeeded)
        {
            _currentProfile = HapticProfileValidator.Validate(profile).Profile;
            ApplyProfileToControls(_currentProfile);
            ApplyProfileToRuntime(_currentProfile);
            _profileTuningController.RefreshEffectSettings(_currentProfile);
        }
        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildSaveProfilesFeedback(result, phprResult);

        UpdateProfileStatus(feedback.ProfileStatusMessage, feedback.ProfileValidationMessages);
        FooterStatusText.Text = feedback.FooterStatusText ?? string.Empty;
    }

    private async void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _profileStore.LoadAsync(HapticProfileStore.GetDefaultProfilePath());
        var phprResult = await _phprProfileStore.LoadAsync(PhprEffectProfileStore.GetDefaultProfilePath());
        if (result.Succeeded && result.Profile is not null)
        {
            ApplyProfileToControls(result.Profile);
            ApplyProfileToRuntime(result.Profile);
            _profileTuningController.RefreshEffectSettings(_currentProfile);
            UpdateEffectStatus();
            UpdateMixerStatus();
        }

        if (phprResult.Succeeded && phprResult.Profile is not null)
        {
            ApplyPhprEffectProfileToRuntime(phprResult.Profile);
            SaveAppSettings();
        }
        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildLoadProfilesFeedback(result, phprResult);

        UpdateProfileStatus(feedback.ProfileStatusMessage, feedback.ProfileValidationMessages);
        FooterStatusText.Text = feedback.FooterStatusText ?? string.Empty;
    }

    private void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyProfileToControls(HapticDriveProfile.Default);
        ApplyProfileToRuntime(HapticDriveProfile.Default);
        ApplyPhprEffectProfileToRuntime(PhprEffectProfile.Default);
        ApplyBst1PaddleGearPulseSetting(new Bst1PaddleGearPulseSetting());
        ApplyBst1PulseSettingsToControls();
        _profileTuningController.RefreshEffectSettings(_currentProfile);
        var audioSaveResult = PersistCurrentAudioProfile();
        SaveAppSettings();
        UpdateEffectStatus();
        UpdateMixerStatus();
        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildResetFeedback(audioSaveResult, _hapticsStarted);
        UpdateProfileStatus(feedback.ProfileStatusMessage, feedback.ProfileValidationMessages);
        FooterStatusText.Text = feedback.FooterStatusText ?? string.Empty;
    }

    private void RefreshDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Diagnostics refreshed.";
    }

    private void RoadTextureFlightRecorderCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (RoadTextureFlightRecorderCheckBox.IsChecked == true)
        {
            _roadTextureFlightRecorder = new FileRoadTextureFlightRecorder(GetLocalValidationResultsDirectory());
            FooterStatusText.Text = $"Road texture flight recorder enabled: {_roadTextureFlightRecorder.LogPath}";
        }
        else
        {
            _roadTextureFlightRecorder = DisabledRoadTextureFlightRecorder.Instance;
            FooterStatusText.Text = "Road texture flight recorder disabled.";
        }

        UpdateDiagnosticsStatus();
    }

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var presentation = BuildDiagnosticsStatusPresentation();
        ApplyDiagnosticsStatusPresentation(presentation);

        try
        {
            Clipboard.SetText(presentation.ClipboardReportText);
            FooterStatusText.Text = "Diagnostics report copied.";
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = $"Diagnostics report could not be copied: {ex.Message}";
        }
    }

    private async void TestBenchSignalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TestBenchSignalComboBox.SelectedItem is not AudioTestSignalDefinition signal)
        {
            return;
        }

        _testBench.SelectSignal(signal);

        if (_testBench.GetSnapshot().IsActive)
        {
            var renderResult = await _testBench.RenderNextBufferAsync();
            FooterStatusText.Text = renderResult.Message;
        }

        UpdateTestBenchStatus();
    }

    private async void TestBenchStartStopButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _testBench.GetSnapshot();
        AudioTestBenchOperationResult result;

        if (snapshot.IsActive)
        {
            result = await _testBench.StopAsync();
        }
        else
        {
            if (TestBenchSignalComboBox.SelectedItem is AudioTestSignalDefinition signal)
            {
                _testBench.SelectSignal(signal);
            }

            _testBench.EmergencyMute = _emergencyMuted;
            var startResult = await _testBench.StartAsync();
            result = startResult.Succeeded
                ? await _testBench.RenderNextBufferAsync()
                : startResult;
        }

        FooterStatusText.Text = result.Message;
        UpdateTestBenchStatus();
    }

    private void ManualBst1Control_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyManualBst1SettingsFromControls("Manual BST-1 pulse settings updated.");
    }

    private void Bst1PaddleGearPulseControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingBst1PulseUi)
        {
            return;
        }

        ApplyBst1PaddleGearPulseSettingsFromControls("BST-1 paddle gear pulse settings updated.");
    }

    private void Bst1PaddleGearPulseControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingBst1PulseUi)
        {
            return;
        }

        ApplyBst1PaddleGearPulseSettingsFromControls("BST-1 paddle gear pulse settings updated.");
    }

    private async void ManualBst1PulseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyManualBst1SettingsFromControls("Manual BST-1 pulse settings ready."))
        {
            return;
        }

        await StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(
            _manualBst1FrequencyHz,
            TimeSpan.FromMilliseconds(_manualBst1DurationMs),
            _manualBst1StrengthPercent / 100f,
            _bst1OutputTrimPercent / 100f,
            Source: "manual test",
            DurationMode: "manual"));
    }

    private void ManualAsioHardwareTestChannel0Button_Click(object sender, RoutedEventArgs e)
    {
        SelectManualAsioHardwareTestChannel(0);
    }

    private void ManualAsioHardwareTestChannel1Button_Click(object sender, RoutedEventArgs e)
    {
        SelectManualAsioHardwareTestChannel(1);
    }

    private void ManualAsioHardwareTestBothChannelsButton_Click(object sender, RoutedEventArgs e)
    {
        ManualAsioHardwareStatusText.Text = "Mono/both channel test uses the currently selected single ASIO output channel in this architecture. Select channel 0 or 1 explicitly before starting the test.";
        FooterStatusText.Text = "Manual ASIO mono/both test is diagnostic-only until multi-channel routing exists; no output was started.";
        UpdateManualAsioHardwareTestStatus();
    }

    private async Task StartManualAsioHardwareTestAsync(ManualAsioHardwareTestRequest request)
    {
        var result = await _hapticPipeline.StartManualAsioHardwareTestAsync(request);

        FooterStatusText.Text = result.Message;
        UpdateManualAsioHardwareTestStatus();
        UpdateDiagnosticsStatus();
    }

    private async void SelectManualAsioHardwareTestChannel(int channel)
    {
        var selection = Bst1AsioChannelSelection.Select(channel, _selectedOutputKind);
        _selectedAsioOutputChannel = selection.SelectedChannel;
        AsioOutputChannelComboBox.SelectedItem = selection.SelectedChannel;
        SaveAppSettings();
        if (selection.ShouldRebuildPipeline)
        {
            await RunSerializedLifecycleOperationAsync(
                (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                    generation,
                    selection.Message,
                    cancellationToken),
                "Manual ASIO channel selection failed");
            return;
        }

        UpdateManualAsioHardwareTestStatus();
        FooterStatusText.Text = selection.Message;
    }

    private void UpdateTestBenchStatus()
    {
        UpdateTestingValidationPresentation();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Testing / Validation" })
        {
            PageStatusText.Text = BuildTestingValidationStatusPresentation().TestingValidationPageStatusText;
        }

        UpdateDiagnosticsStatus();
    }

    private void UpdateManualAsioHardwareTestStatus()
    {
        var snapshot = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        ManualAsioHardwareStatusText.Text =
            $"BST-1 channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}; driver {snapshot.SelectedAsioDriver}; armed {snapshot.AsioArmed}; haptics {(snapshot.HapticsRunning ? "running" : "stopped")}; peak {snapshot.ManualPulsePeak:0.000}.";
        ManualAsioHardwareBlockedReasonText.Text = snapshot.BlockedReason is null
            ? $"{Bst1AsioStatusFormatter.FormatLastPulseCompact(snapshot)}; {_lastBst1PaddleGearPulseMessage}"
            : Bst1AsioStatusFormatter.FormatLastPulseCompact(snapshot);
        UpdateDevicesPresentation();
    }

    private void ApplyBst1PulseSettingsToControls()
    {
        var values = ControlSettingsSnapshotBuilder.BuildBst1PulseControlValues(
            _manualBst1StrengthPercent,
            _bst1OutputTrimPercent,
            _manualBst1FrequencyHz,
            _manualBst1DurationMs,
            _bst1PaddleGearPulseEnabled,
            _bst1PaddleGearStrengthPercent,
            _bst1PaddleGearFrequencyHz,
            _bst1PaddleGearSyncDuration,
            _sharedPhprGearPulseDurationMs,
            _bst1PaddleGearCustomDurationMs,
            GetEffectiveBst1PaddleGearDurationMs());
        _updatingBst1PulseUi = true;
        ManualBst1StrengthTextBox.Text = values.ManualStrengthText;
        Bst1OutputTrimTextBox.Text = values.OutputTrimText;
        ManualBst1FrequencyTextBox.Text = values.ManualFrequencyText;
        ManualBst1DurationTextBox.Text = values.ManualDurationText;
        Bst1PaddleGearPulseEnabledCheckBox.IsChecked = values.PaddleGearPulseEnabled;
        Bst1PaddleGearStrengthTextBox.Text = values.PaddleGearStrengthText;
        Bst1PaddleGearFrequencyTextBox.Text = values.PaddleGearFrequencyText;
        Bst1PaddleGearSyncDurationCheckBox.IsChecked = values.PaddleGearSyncDuration;
        Bst1PaddleGearDurationTextBox.Text = values.PaddleGearDurationText;
        Bst1PaddleGearDurationTextBox.IsEnabled = values.PaddleGearDurationEnabled;
        Bst1PaddleGearEffectiveDurationText.Text = values.PaddleGearEffectiveDurationText;
        _updatingBst1PulseUi = false;
    }

    private bool ApplyManualBst1SettingsFromControls(string footerMessage)
    {
        if (!ControlSettingsSnapshotBuilder.TryBuildBst1ManualPulseSettings(
                new Bst1ManualPulseControlInputs(
                    StrengthText: ManualBst1StrengthTextBox.Text,
                    OutputTrimText: Bst1OutputTrimTextBox.Text,
                    FrequencyText: ManualBst1FrequencyTextBox.Text,
                    DurationText: ManualBst1DurationTextBox.Text),
                out var settings,
                out var message))
        {
            ManualAsioHardwareStatusText.Text = message;
            FooterStatusText.Text = message;
            return false;
        }

        _manualBst1StrengthPercent = settings.StrengthPercent;
        _bst1OutputTrimPercent = settings.OutputTrimPercent;
        _manualBst1FrequencyHz = settings.FrequencyHz;
        _manualBst1DurationMs = settings.DurationMs;
        ApplyBst1PulseSettingsToControls();
        UpdateManualAsioHardwareTestStatus();
        FooterStatusText.Text = footerMessage;
        return true;
    }

    private bool ApplyBst1PaddleGearPulseSettingsFromControls(string footerMessage)
    {
        if (!ControlSettingsSnapshotBuilder.TryBuildBst1PaddleGearPulseSettings(
                new Bst1PaddleGearPulseControlInputs(
                    IsEnabled: Bst1PaddleGearPulseEnabledCheckBox.IsChecked == true,
                    StrengthText: Bst1PaddleGearStrengthTextBox.Text,
                    FrequencyText: Bst1PaddleGearFrequencyTextBox.Text,
                    UseSharedDuration: Bst1PaddleGearSyncDurationCheckBox.IsChecked == true,
                    DurationText: Bst1PaddleGearDurationTextBox.Text,
                    ExistingCustomDurationMs: _bst1PaddleGearCustomDurationMs),
                out var settings,
                out var message))
        {
            ManualAsioHardwareStatusText.Text = message;
            FooterStatusText.Text = message;
            return false;
        }

        _bst1PaddleGearPulseEnabled = settings.IsEnabled;
        _bst1PaddleGearStrengthPercent = settings.StrengthPercent;
        _bst1PaddleGearFrequencyHz = settings.FrequencyHz;
        _bst1PaddleGearSyncDuration = settings.UseSharedDuration;
        _bst1PaddleGearCustomDurationMs = settings.CustomDurationMs;
        _lastBst1PaddleGearPulseMessage = settings.StatusMessage;
        ApplyBst1PulseSettingsToControls();
        SaveAppSettings();
        UpdateManualAsioHardwareTestStatus();
        FooterStatusText.Text = footerMessage;
        return true;
    }

    private static string BuildTrueAsioStatusText(ManualAsioHardwareTestSnapshot snapshot)
    {
        return Bst1AsioStatusFormatter.FormatCompact(snapshot);
    }

    private static string BuildTrueAsioDiagnosticsText(ManualAsioHardwareTestSnapshot snapshot)
    {
        return Bst1AsioStatusFormatter.FormatDetailed(snapshot);
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        return duration is null
            ? "none"
            : $"{duration.Value.TotalMilliseconds:0.000} ms";
    }

    private static string FormatMilliseconds(double? milliseconds)
    {
        return milliseconds is null
            ? "none"
            : $"{milliseconds.Value:0.000} ms";
    }

    private void UpdateOutputStatus(AudioOutputStatus status)
    {
        _audioOutputController.Publish(
            BuildSelectedOutputId(),
            status.StatusMessage,
            status.SampleRate,
            status.BufferSize);
        UpdateDashboardStatus();
        UpdateDeviceStatus();
    }

    private void UpdateMixerStatus()
    {
        var presentation = BuildRoutingMixerStatusPresentation();
        RoutingMixerViewControl.Apply(presentation);

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Routing / Mixer" })
        {
            PageStatusText.Text = presentation.RoutingMixerPageStatusText;
        }
    }

    private async void ExportSupportBundleButton_Click(object sender, RoutedEventArgs e)
    {
        var presentation = BuildDiagnosticsStatusPresentation();
        ApplyDiagnosticsStatusPresentation(presentation);
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var redactionMode = ExtendedSupportBundleDiagnosticsCheckBox.IsChecked == true
            ? HapticDrive.Asio.Core.Diagnostics.DiagnosticRedactionMode.Extended
            : HapticDrive.Asio.Core.Diagnostics.DiagnosticRedactionMode.Safe;

        try
        {
            string? selectedRecordingFileName = null;
            string? selectedRecordingDetailText = null;
            if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem item)
            {
                selectedRecordingFileName = Path.GetFileName(item.Path);
                var analysisText = await GetRecordingLibraryAnalysisTextAsync(item).ConfigureAwait(true);
                selectedRecordingDetailText = RecordingLibraryDetailFormatter.BuildClipboardText(
                    item.Path,
                    item.DisplayText,
                    item.DetailText,
                    analysisText);
            }

            var directory = GetLocalValidationResultsDirectory();
            var structuredDiagnostics = BuildSupportBundleStructuredDiagnostics(generatedAtUtc);
            var inputs = new SupportBundleExportInputs(
                generatedAtUtc,
                GameTelemetryCatalog.NormalizeGameId(_selectedGameId),
                GameTelemetryCatalog.GetDisplayName(_selectedGameId),
                presentation,
                structuredDiagnostics,
                redactionMode,
                selectedRecordingFileName,
                selectedRecordingDetailText);
            _lastSupportBundleExportPath = _supportBundleExporter.ExportZip(inputs, directory);
            FooterStatusText.Text = $"Support bundle exported locally to {_lastSupportBundleExportPath} in {redactionMode} mode.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = $"Support bundle export failed safely: {ex.Message}";
        }
    }

    private RoutingMixerStatusPresentation BuildRoutingMixerStatusPresentation()
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        var effectSnapshot = pipelineSnapshot.Effects;
        var audioDiagnostics = AudioRuntimeDiagnosticsSnapshot.Create(
            pipelineSnapshot.Output,
            effectSnapshot,
            pipelineSnapshot.Audio,
            _testBench.GetSnapshot());
        var mixer = _currentProfile.ToMixerSettings(_emergencyMuted);
        var safety = _currentProfile.ToSafetyOptions(_emergencyMuted);
        var manualAsio = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        var selectedOutputMode = OutputModeComboBox.SelectedItem is OutputModeOption selectedMode
            ? selectedMode.Label
            : pipelineSnapshot.Output.DisplayName;

        ConfigurePhprDirectRuntime();
        var directRuntime = _phprDirectRuntime.GetSnapshot();
        var realOutput = _realPhprOutput.GetDiagnostics();
        var continuousRuntime = _realPhprContinuousEffectsRuntime.GetSnapshot();
        var realSlipLockSnapshot = _realSlipLockRouter.GetSnapshot();
        var brakeSlipLockActive = realSlipLockSnapshot.ActiveSlipLockModules is "Brake" or "Both"
            && realSlipLockSnapshot.WheelLock.LastActive;
        var throttleSlipLockActive = realSlipLockSnapshot.ActiveSlipLockModules is "Throttle" or "Both"
            && realSlipLockSnapshot.WheelSlip.LastActive;
        var directReadiness = directRuntime.DirectReady
            ? "direct ready"
            : $"direct blocked: {(string.IsNullOrWhiteSpace(directRuntime.BlockedReason) ? "safety gate" : directRuntime.BlockedReason)}";

        return RoutingMixerStatusPresenter.Build(
            RoutingMixerStatusSnapshotBuilder.Build(new RoutingMixerStatusBuildInputs(
                MasterGain: mixer.MasterGain,
                SafetyOutputGain: safety.OutputGain,
                EmergencyMuted: _emergencyMuted,
                NormalMuted: pipelineSnapshot.IsMuted,
                OutputPeakLevel: audioDiagnostics.OutputPeakLevel,
                MixerPeakLevel: audioDiagnostics.MixerPeakLevel,
                LimitedSampleCount: audioDiagnostics.LimitedSampleCount,
                ClippedSampleCount: audioDiagnostics.ClippedSampleCount,
                SelectedOutputModeText: selectedOutputMode,
                SelectedAsioDriverNameText: _selectedAsioDriverName ?? "none",
                SelectedAsioOutputChannelText: _selectedAsioOutputChannel is null ? "none" : _selectedAsioOutputChannel.ToString()!,
                AsioArmed: _asioArmed,
                TrueAsioStatusText: BuildTrueAsioStatusText(manualAsio),
                Bst1GearEnabled: _bst1PaddleGearPulseEnabled,
                EffectSnapshot: effectSnapshot,
                PhprPedalsModeText: PhprPedalsModeComboBox.SelectedItem?.ToString() ?? "none",
                BrakeGearPulseEnabled: _realPhprOptions.BrakeGearPulse.IsEnabled,
                DirectReadinessText: directReadiness,
                DirectConnectionStateText: realOutput.Connection.State.ToString(),
                BrakeGearActive: directRuntime.HardwareBelievedActive,
                BrakeRoadEnabled: _realRoadVibrationOptions.IsEnabled && _realRoadVibrationOptions.Brake.IsEnabled,
                BrakeRoadActive: continuousRuntime.LastRoadVibrationRoutingResult?.WasRouted == true,
                BrakeLockEnabled: _realSlipLockOptions.IsEnabled && _realSlipLockOptions.WheelLock.IsEnabled,
                BrakeLockActive: brakeSlipLockActive,
                ThrottleGearPulseEnabled: _realPhprOptions.ThrottleGearPulse.IsEnabled,
                PhprSoftwareCoexistenceStatusText: _phprSoftwareCoexistenceSnapshot.Status.ToString(),
                RealEmergencyStopActive: realOutput.Output.IsEmergencyStopActive,
                ThrottleGearActive: directRuntime.HardwareBelievedActive,
                ThrottleRoadEnabled: _realRoadVibrationOptions.IsEnabled && _realRoadVibrationOptions.Throttle.IsEnabled,
                ThrottleRoadActive: continuousRuntime.LastRoadVibrationRoutingResult?.WasRouted == true,
                ThrottleSlipEnabled: _realSlipLockOptions.IsEnabled && _realSlipLockOptions.WheelSlip.IsEnabled,
                ThrottleSlipActive: throttleSlipLockActive)));
    }

    private void UpdateDeviceStatus()
    {
        var snapshot = _hapticPipeline.GetSnapshot();
        var status = snapshot.Output;
        var presentation = BuildDevicesStatusPresentation();
        DevicesViewControl.Apply(presentation);
        UpdateManualAsioHardwareTestStatus();
        UpdatePhprSoftwareCoexistenceStatus();
        UpdatePhprControlledWriteReadinessStatus();
        UpdateRealPhprDirectControlStatus();
        UpdatePhprValidationStatus();
        UpdatePhprWorkflowStatus();
        UpdateInputDiscoveryStatus();
        UpdatePaddleInputStatus();
        UpdateShiftIntentStatus();
        UpdatePaddleGearBenchStatus();
        UpdateLocalGearTestStatus();
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdatePhprPedalsStatus();
        UpdateDashboardStatus();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Devices" })
        {
            PageStatusText.Text = presentation.DevicesPageStatusText;
        }
    }

    private void UpdateDevicesPresentation()
    {
        DevicesViewControl.Apply(BuildDevicesStatusPresentation());
    }

    private DevicesStatusPresentation BuildDevicesStatusPresentation()
    {
        var outputSnapshot = _hapticPipeline.GetSnapshot().Output;
        var manualAsioSnapshot = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        var inputDiscoverySnapshot = _inputDiscoverySnapshot;
        var paddleSnapshot = _paddleInputSource.GetPaddleSnapshot();
        var selectedComboItem = PaddleInputDeviceComboBox.SelectedItem as PaddleDeviceListItem;
        var selectionBlocker = BuildPaddleInputSelectionBlocker();
        var selectedText = paddleSnapshot.SelectedDevice is null
            ? selectedComboItem is not null
                ? selectedComboItem.DisplayText
                : _paddleMapping.SelectedDeviceId is null ? "none" : $"saved {_paddleMapping.SelectedDeviceId}"
            : $"{paddleSnapshot.SelectedDevice.DisplayName} ({paddleSnapshot.SelectedDevice.Method})";
        var selectedButtonCount = selectedComboItem is null
            ? "none"
            : $"{PaddleInputDeviceSelector.GetUsableButtonCount(selectedComboItem.Device):N0}";
        var shiftIntentDiagnostics = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var drivingSnapshot = _drivingArmedStateService.GetSnapshot();
        var wheelInputCandidates = inputDiscoverySnapshot.HasRun
            ? _wheelInputCandidateProvider.GetCandidates(inputDiscoverySnapshot)
            : [];

        return DevicesStatusPresenter.Build(new DevicesStatusSnapshot(
            CurrentOutputDisplayName: outputSnapshot.DisplayName,
            CurrentOutputState: outputSnapshot.State.ToString(),
            CurrentOutputStatusMessage: outputSnapshot.StatusMessage,
            AsioVisibilityMessage: _asioVisibilitySnapshot.Message,
            TrueAsioStatusText: BuildTrueAsioStatusText(manualAsioSnapshot),
            SelectedAsioDriverName: _selectedAsioDriverName,
            SelectedAsioOutputChannel: _selectedAsioOutputChannel,
            AsioArmed: _asioArmed,
            HardwareChainWarning: _asioReadinessSnapshot.HardwareChainWarning,
            OutputRequiresPhysicalHardware: outputSnapshot.RequiresPhysicalHardware,
            InputDiscoveryHasRun: inputDiscoverySnapshot.HasRun,
            InputDiscoveryLocalRefreshText: inputDiscoverySnapshot.HasRun ? inputDiscoverySnapshot.DiscoveredAtUtc.ToLocalTime().ToString("g") : null,
            InputDiscoveryDeviceCount: inputDiscoverySnapshot.DeviceCount,
            InputDiscoveryReadOnlyDiscoverySucceeded: inputDiscoverySnapshot.ReadOnlyDiscoverySucceeded,
            InputDiscoveryLikelyCandidatesText: FormatInputCandidates(inputDiscoverySnapshot.LikelyGtNeoWheelInputCandidates),
            InputDiscoverySavedCandidatesText: FormatInputCandidates(wheelInputCandidates),
            InputDiscoveryMethodText: FormatDiscoveryMethods(inputDiscoverySnapshot.Methods),
            InputDiscoveryErrorText: inputDiscoverySnapshot.Errors.Count == 0 ? "none" : string.Join("; ", inputDiscoverySnapshot.Errors),
            PaddleListenerStatus: paddleSnapshot.Status,
            PaddleSelectedText: selectedText,
            PaddlePressCount: paddleSnapshot.PaddlePressCount,
            PaddleLastMappedText: paddleSnapshot.LastPaddleEvent is null
                ? "none"
                : $"{paddleSnapshot.LastPaddleEvent.PaddleSide} paddle button {paddleSnapshot.LastPaddleEvent.ButtonId} at {paddleSnapshot.LastPaddleEvent.TimestampUtc.ToLocalTime():T}",
            PaddleLeftButtonMappingText: FormatButtonMapping(paddleSnapshot.Mapping.LeftPaddleButtonId),
            PaddleRightButtonMappingText: FormatButtonMapping(paddleSnapshot.Mapping.RightPaddleButtonId),
            PaddleDebounceMilliseconds: paddleSnapshot.Mapping.DebounceDuration.TotalMilliseconds,
            PaddleSelectedButtonCountText: selectedButtonCount,
            PaddleLastRawText: paddleSnapshot.LastChangedButtonId is null
                ? "none"
                : $"button {paddleSnapshot.LastChangedButtonId} {paddleSnapshot.LastChangedButtonState}",
            PaddleErrorText: paddleSnapshot.LastErrorMessage ?? "none",
            PaddleSelectionBlocker: selectionBlocker,
            ShiftIntentEnabled: shiftIntentDiagnostics.IsEnabled,
            ShiftIntentModeText: shiftIntentDiagnostics.Mode.ToString(),
            DrivingArmed: drivingSnapshot.Current.IsArmed,
            ShiftIntentAcceptedCount: shiftIntentDiagnostics.AcceptedShiftIntentCount,
            ShiftIntentSuppressedCount: shiftIntentDiagnostics.SuppressedShiftIntentCount,
            ShiftIntentTelemetryAgeText: drivingSnapshot.LastTelemetryAge is null
                ? "none"
                : $"{drivingSnapshot.LastTelemetryAge.Value.TotalMilliseconds:0} ms",
            ShiftIntentMenuSafeModeEnabled: drivingSnapshot.MenuSafeModeEnabled,
            ShiftIntentLastAcceptedText: shiftIntentDiagnostics.LastAcceptedEvent is null
                ? "none"
                : $"{shiftIntentDiagnostics.LastAcceptedEvent.Direction} at {shiftIntentDiagnostics.LastAcceptedEvent.TimestampUtc.ToLocalTime():T}, gear {FormatOptionalInt(shiftIntentDiagnostics.LastAcceptedEvent.LastTelemetryGear)}",
            ShiftIntentLastSuppressedText: shiftIntentDiagnostics.LastSuppressedEvent is null
                ? "none"
                : $"{shiftIntentDiagnostics.LastSuppressedEvent.PaddleEvent.PaddleSide} at {shiftIntentDiagnostics.LastSuppressedEvent.EvaluatedAtUtc.ToLocalTime():T}: {shiftIntentDiagnostics.LastSuppressedEvent.SuppressionReason}",
            SelectedPhprModeText: GetSelectedPhprPedalsMode().ToString()));
    }

    private void UpdateProfileStatus(string? message = null, IReadOnlyList<string>? validationMessages = null)
    {
        UpdateProfilesPresentation(message, validationMessages);
        var settingsPresentation = BuildPersistedSettingsStatusPresentation();
        SettingsStatusText.Text = settingsPresentation.StatusText;
        SettingsPathText.Text = settingsPresentation.PathText;
        RuntimePrerequisiteText.Text = $".NET Desktop runtime is available for this running WPF app. Launch script sets DOTNET_ROOT to the repo-local .NET 8 runtime before starting the executable.";
        UpdateDashboardStatus();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Profiles" })
        {
            PageStatusText.Text = BuildProfilesStatusPresentation(message, validationMessages).ProfilesPageStatusText;
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            PageStatusText.Text = $"Theme {(_lightTheme ? "light" : "dark")}; app settings are local; ASIO readiness and safe P-HPR mode preferences can persist without starting output, while live runtime/device state remains non-persistent.";
        }
    }

    private PersistedSettingsStatusPresentation BuildPersistedSettingsStatusPresentation()
    {
        var shiftIntent = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var mockGear = _mockGearPulseRouter.GetSnapshot().Options;
        var mockPedalEffects = _mockPedalEffectsRouter.GetSnapshot().Options;
        return PersistedSettingsStatusPresenter.Build(new PersistedSettingsStatusSnapshot(
            SettingsPath: _settingsStore.SettingsPath,
            SettingsError: _settingsError,
            UseLightTheme: _lightTheme,
            ActiveProfileName: _currentProfile.Name,
            SelectedGameId: _selectedGameId,
            SelectedGameDisplayName: GameTelemetryCatalog.GetDisplayName(_selectedGameId),
            SelectedOutputKind: _selectedOutputKind,
            PhprPedalsEnabledPreference: _phprPedalsEnabledPreference,
            PhprPedalsModePreference: _phprPedalsModePreference,
            ReplayTimingLabel: GetSelectedReplayTimingMode().Label,
            ForwardingDestinationCount: _forwardingDestinations.Count,
            SelectedAsioDriverName: _selectedAsioDriverName,
            SelectedAsioOutputChannel: _selectedAsioOutputChannel,
            ArmAsioPreference: _asioArmed,
            PaddleMapping: _paddleMapping,
            Bst1PaddleGearPulseEnabled: _bst1PaddleGearPulseEnabled,
            Bst1PaddleGearStrengthPercent: _bst1PaddleGearStrengthPercent,
            Bst1PaddleGearFrequencyHz: _bst1PaddleGearFrequencyHz,
            EffectiveBst1PaddleGearDurationMs: GetEffectiveBst1PaddleGearDurationMs(),
            ShiftIntentEnabled: shiftIntent.IsEnabled,
            ShiftIntentMode: shiftIntent.Mode,
            RealDirectControlEnabled: _realPhprOptions.DirectControlEnabled,
            RealRoadVibrationEnabled: _realRoadVibrationOptions.IsEnabled,
            RealSlipLockEnabled: _realSlipLockOptions.IsEnabled,
            MockGearRoutingEnabled: mockGear.IsEnabled,
            MockGearRoutingTarget: mockGear.TargetModule,
            MockPedalEffectsEnabled: mockPedalEffects.IsEnabled));
    }

    private void UpdateInputDiscoveryStatus()
    {
        UpdateDevicesPresentation();
    }

    private void UpdatePaddleInputStatus()
    {
        UpdateDevicesPresentation();
    }

    private string? BuildPaddleInputSelectionBlocker()
    {
        if (!_inputDiscoverySnapshot.HasRun)
        {
            return "input discovery has not completed";
        }

        if (PaddleInputDeviceComboBox.SelectedItem is not PaddleDeviceListItem selected)
        {
            if (_paddleDeviceItems.Count == 0)
            {
                return "no Windows game-controller input devices were discovered";
            }

            if (!_paddleDeviceItems.Any(item => PaddleInputDeviceSelector.HasUsableButtons(item.Device)))
            {
                return "only 0-button Windows game-controller candidates were discovered; the listener is blocked until a usable button-capable device appears";
            }

            return "no usable Windows game-controller input device is selected";
        }

        if (!PaddleInputDeviceSelector.HasUsableButtons(selected.Device))
        {
            return $"{selected.DisplayText} exposes 0 usable buttons; 0-button controllers are blocked";
        }

        return null;
    }

    private void UpdateShiftIntentStatus()
    {
        UpdateDevicesPresentation();
    }

    private void UpdatePaddleGearBenchStatus()
    {
        var snapshot = _paddleGearBenchTestController.GetSnapshot();
        var options = snapshot.Options;
        var lastAccepted = snapshot.LastAcceptedBenchEvent is null
            ? "none"
            : $"{snapshot.LastAcceptedBenchEvent.Direction} {snapshot.LastAcceptedBenchEvent.PaddleSide} button {snapshot.LastAcceptedBenchEvent.SourceButtonId?.ToString() ?? "unknown"} at {snapshot.LastAcceptedBenchEvent.TimestampUtc.ToLocalTime():T}";
        var lastBenchSource = snapshot.LastPaddleEvent is null
            ? "none"
            : $"listener device {snapshot.LastPaddleEvent.SourceDevice?.DeviceId ?? "unknown"}; event {snapshot.LastPaddleEvent.ButtonState}; mapped side {snapshot.LastPaddleEvent.PaddleSide}; mapped button {snapshot.LastPaddleEvent.ButtonId}; sequence {snapshot.LastPaddleEvent.SequenceNumber:N0}";
        var lastBenchDecision = snapshot.LastResult is null
            ? "none"
            : snapshot.LastResult.Accepted
                ? $"accepted: {snapshot.LastResult.Message}"
                : $"rejected: {snapshot.LastResult.SuppressionReason ?? snapshot.LastResult.Message}";
        ConfigurePhprDirectRuntime();
        var runtime = _phprDirectRuntime.GetSnapshot();
        var realOutputSnapshot = _realPhprOutput.GetSnapshot();
        var realDiagnostics = _realPhprOutput.GetDiagnostics();
        var paddleSnapshot = _paddleInputSource.GetPaddleSnapshot();
        var directReady = runtime.DirectReady;
        var directMessage = string.IsNullOrWhiteSpace(runtime.BlockedReason)
            ? "runtime ready"
            : runtime.BlockedReason;
        var benchRetriggerMode = options.OutputMode == PaddleGearBenchTestOutputMode.Direct
            ? PHprGearPulseRetriggerMode.RetriggerLatestPressWins
            : realDiagnostics.Options.GearPulseRetriggerMode;

        PaddleGearBenchStatusText.Text =
            $"Paddle bench: {(options.IsEnabled ? "enabled" : "disabled")}; {(options.IsArmed ? "auto-armed" : "blocked")}; output {options.OutputMode}; target {options.TargetModule}; runtime {runtime.State}; direct {(directReady ? "ready" : $"blocked: {directMessage}")}; active pulse {runtime.HardwareBelievedActive}; pending stops {runtime.PendingStopCount:N0}.";
        PaddleGearBenchItemsControl.ItemsSource = new[]
        {
            "Safety: local validation only. Normal live-driving shift intent still requires DrivingArmed and recent telemetry outside this bench mode.",
            $"Enabled: {options.IsEnabled}; direct ready: {directReady}; paddle listener: {paddleSnapshot.Status}; mapped left {FormatButtonMapping(_paddleMapping.LeftPaddleButtonId)}, right {FormatButtonMapping(_paddleMapping.RightPaddleButtonId)}.",
            $"Last accepted paddle: {lastAccepted}; left accepted {snapshot.LeftPaddleAcceptedCount:N0}; right accepted {snapshot.RightPaddleAcceptedCount:N0}; suppressed {snapshot.SuppressedBenchGearEventCount:N0}.",
            $"Bench event source: {lastBenchSource}.",
            $"Bench accepted/rejected reason: {lastBenchDecision}.",
            $"Runtime path proof: service {runtime.SharedPathProof.SameServiceInstance}; writer {runtime.SharedPathProof.SameWriterInstance}; encoder {runtime.SharedPathProof.SameEncoder}; stop method {runtime.SharedPathProof.SameStopMethod}; pulse id {runtime.PulseId:N0}.",
            $"Bench pulse source: target {options.TargetModule}; route service {(runtime.SharedPathProof.IsProven ? PhprDeviceCardPulseService.RouteName : "blocked")}; brake {FormatRealPhprPulse(_realPhprOptions.BrakeGearPulse)}; throttle {FormatRealPhprPulse(_realPhprOptions.ThrottleGearPulse)}.",
            $"Direct pulse diagnostics: active pulse {realDiagnostics.ActivePulse}; pending stop count {realOutputSnapshot.PendingScheduledStopCount:N0}; scheduled duration {realDiagnostics.LastScheduledPulseDurationMs?.ToString(CultureInfo.InvariantCulture) ?? "none"} ms; accepted latency {FormatMilliseconds(runtime.Latency.PaddleReceivedToBenchAcceptedMs)}; write latency {FormatMilliseconds(runtime.Latency.PaddleReceivedToStartWriteCompletedMs)}; inter-press {FormatMilliseconds(runtime.Latency.InterPressIntervalMs)}; scheduled stop {FormatTimestamp(runtime.Latency.StopDueUtc ?? realDiagnostics.LastScheduledStopDueAtUtc)}; actual stop {FormatTimestamp(runtime.Latency.StopWriteCompletedUtc ?? realDiagnostics.LastStopSentAtUtc)}.",
            $"Retrigger diagnostics: mode {benchRetriggerMode}; generations brake {realDiagnostics.BrakePulseGeneration:N0}, throttle {realDiagnostics.ThrottlePulseGeneration:N0}, latest {realDiagnostics.LastPulseGeneration:N0}; retriggers {realDiagnostics.RetriggerCount:N0}; stale stops ignored {realDiagnostics.StaleStopIgnoredCount:N0}; busy rejected {realDiagnostics.BusyRejectedCount:N0}; stale output dropped {realDiagnostics.StaleOutputDroppedCount:N0}; debounce suppressed {paddleSnapshot.DebounceSuppressedCount:N0}.",
            $"Last start sent: {FormatTimestamp(realDiagnostics.LastStartSentAtUtc)} target {realDiagnostics.LastStartReportTarget?.ToString() ?? "none"}; last stop sent: {FormatTimestamp(realDiagnostics.LastStopSentAtUtc)} target {realDiagnostics.LastStopReportTarget?.ToString() ?? "none"}; stop result {realDiagnostics.LastStopResultStatus?.ToString() ?? "none"} {realDiagnostics.LastStopResultMessage ?? "none"}.",
            $"Emergency stop: requested {FormatTimestamp(realDiagnostics.LastEmergencyStopRequestedAtUtc)}; result {realDiagnostics.LastEmergencyStopResultStatus?.ToString() ?? "none"} {realDiagnostics.LastEmergencyStopResultMessage ?? "none"}; watchdog stop-all {realDiagnostics.WatchdogStopAllCount:N0} last {FormatTimestamp(realDiagnostics.LastWatchdogStopAllAtUtc)} {realDiagnostics.LastWatchdogStopAllMessage ?? "none"}.",
            $"Last output target: {realDiagnostics.LastTarget?.ToString() ?? "none"}; last report state: {realDiagnostics.LastReportState?.ToString() ?? "none"}; pending stops {realOutputSnapshot.PendingScheduledStopCount:N0}.",
            $"Recovery files: flight recorder {runtime.FlightRecorderPath}; unclean marker {runtime.UncleanShutdownMarkerPath}; marker exists {runtime.UncleanShutdownMarkerExists}.",
            $"Output mode/status: {options.OutputMode}; last output {snapshot.LastOutputStatus ?? "none"}.",
            $"Direct output readiness: {(directReady ? "ready" : $"blocked: {directMessage}")}. Direct bench output requires FeatureReport, report ID 0xF1, 64-byte shape, open-check, clear coexistence, clear emergency stop, and road/slip-lock disabled.",
            $"Last suppression reason: {snapshot.LastSuppressionReason ?? "none"}; last error {snapshot.LastError ?? "none"}."
        };
    }

    private void UpdateMockGearPulseStatus()
    {
        var snapshot = _mockGearPulseRouter.GetSnapshot();
        var options = snapshot.Options;
        var lastDirection = snapshot.LastShiftDirection;
        var lastCommand = FormatPhprCommand(snapshot.LastCommand);
        var lastSafety = snapshot.LastSafetyDecision is null
            ? "none"
            : $"{snapshot.LastSafetyDecision.Kind}: {snapshot.LastSafetyDecision.Message}";
        var lastViolation = snapshot.LastSafetyViolation?.Code.ToString() ?? "none";
        var lastResult = snapshot.LastResult is null
            ? "none"
            : $"{snapshot.LastResult.Status}: {snapshot.LastResult.Message}";

        MockGearPulseStatusText.Text =
            $"Mock gear routing: {(options.IsEnabled ? "enabled" : "disabled")}; target {options.TargetModule}; strength {PhprUiValueConverter.FormatPercent(options.Profile.Strength01)}%; frequency {PhprUiValueConverter.FormatFrequency(options.Profile.FrequencyHz)} Hz; duration {options.Profile.DurationMs} ms; routed {snapshot.AcceptedRouteCount:N0}; ignored {snapshot.IgnoredRouteCount:N0}; safety rejected {snapshot.SafetyRejectedCount:N0}.";
        MockGearPulseItemsControl.ItemsSource = new[]
        {
            "Safety: mock only, no hardware output. Stage 2M does not send USB commands, HID output reports, HID feature reports, or real P-HPR vibration.",
            $"Default source: {PHprCommandSource.PaddleShiftIntent}; target {options.TargetModule}; same pulse for upshift and downshift.",
            $"Last shift direction: {lastDirection}; last target {snapshot.LastTargetModule?.ToString() ?? "none"}",
            $"Last PHprCommand: {lastCommand}",
            $"Last routing result: {lastResult}",
            $"Safety decision: {lastSafety}; violation {lastViolation}",
            $"Mock output commands: {snapshot.OutputSnapshot.AcceptedCommandCount:N0}; rejected {snapshot.OutputSnapshot.RejectedCommandCount:N0}; frames {snapshot.OutputSnapshot.GeneratedFrameCount:N0}; pending scheduled stops {snapshot.OutputSnapshot.PendingScheduledStopCount:N0}",
            $"Emergency stop active: {snapshot.EmergencyStopActive}; mock emergency count {snapshot.OutputSnapshot.EmergencyStopCount:N0}; last error {snapshot.LastError ?? "none"}"
        };
    }

    private void UpdateMockPedalEffectsStatus()
    {
        var snapshot = _mockPedalEffectsRouter.GetSnapshot();
        var lastCommand = FormatPhprCommand(snapshot.LastCommand);
        var lastSafety = snapshot.LastSafetyDecision is null
            ? "none"
            : $"{snapshot.LastSafetyDecision.Kind}: {snapshot.LastSafetyDecision.Message}";
        var lastViolation = snapshot.LastSafetyViolation?.Code.ToString() ?? "none";
        var lastResult = snapshot.LastResult is null
            ? "none"
            : $"{snapshot.LastResult.Status}: {snapshot.LastResult.Message}";

        MockPedalEffectsStatusText.Text =
            $"Mock pedal effects: {(snapshot.Options.IsEnabled ? "enabled" : "disabled")}; road {FormatPedalEffectState(snapshot.Options.RoadVibration)}; slip {FormatPedalEffectState(snapshot.Options.WheelSlip)}; lock {FormatPedalEffectState(snapshot.Options.WheelLock)}; evaluations {snapshot.EvaluationCount:N0}; ignored {snapshot.IgnoredEvaluationCount:N0}.";
        MockPedalEffectsItemsControl.ItemsSource = new[]
        {
            "Safety: mock only, no hardware output. Stage 2N does not send USB commands, HID output reports, HID feature reports, ASIO/BST-1 output, or real P-HPR vibration.",
            $"Priority: {PHprPedalEffectKind.WheelLock} > {PHprPedalEffectKind.WheelSlip} > {PHprPedalEffectKind.RoadVibration}; shared mock output with gear routing; emergency stop is global.",
            FormatPedalEffectDiagnostics(snapshot.RoadVibration),
            FormatPedalEffectDiagnostics(snapshot.WheelSlip),
            FormatPedalEffectDiagnostics(snapshot.WheelLock),
            $"Last active effect: {snapshot.LastActiveEffect?.ToString() ?? "none"}; last target {snapshot.LastTargetModule?.ToString() ?? "none"}",
            $"Last PHprCommand: {lastCommand}",
            $"Last routing result: {lastResult}",
            $"Safety decision: {lastSafety}; violation {lastViolation}",
            $"Mock output commands: {snapshot.OutputSnapshot.AcceptedCommandCount:N0}; rejected {snapshot.OutputSnapshot.RejectedCommandCount:N0}; frames {snapshot.OutputSnapshot.GeneratedFrameCount:N0}; pending scheduled stops {snapshot.OutputSnapshot.PendingScheduledStopCount:N0}",
            $"Emergency stop active: {snapshot.EmergencyStopActive}; mock emergency count {snapshot.OutputSnapshot.EmergencyStopCount:N0}; last error {snapshot.LastError ?? "none"}"
        };
    }

    private void UpdatePhprWorkflowStatus()
    {
        var realDiagnostics = _realPhprOutput.GetDiagnostics();
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var presentation = BuildPhprWorkflowStatusPresentation(pipelineSnapshot, realDiagnostics);
        _phprOutputController.Publish(
            presentation.StatusText,
            presentation.ValidationStatusText);

        PhprWorkflowStatusText.Text = presentation.StatusText;
        PhprWorkflowItemsControl.ItemsSource = presentation.Items;
        PhprLiveF1ValidationStatusText.Text = presentation.ValidationStatusText;
        PhprLiveF1ValidationItemsControl.ItemsSource = presentation.ValidationItems;
    }

    private PhprWorkflowStatusPresentation BuildPhprWorkflowStatusPresentation(
        HapticPipelineSnapshot pipelineSnapshot,
        PHprRealOutputDiagnostics realDiagnostics)
    {
        var mockGear = _mockGearPulseRouter.GetSnapshot();
        var mockPedalEffects = _mockPedalEffectsRouter.GetSnapshot();
        var liveValidationSnapshot = BuildPhprLiveF1ValidationSnapshot(pipelineSnapshot, realDiagnostics);
        var snapshot = PhprWorkflowStatusSnapshotBuilder.Build(new PhprWorkflowStatusBuildInputs(
            new PhprWorkflowDiagnosticsSnapshot(
                GetPhprWorkflowModeText(),
                pipelineSnapshot.InputSource.ToString(),
                FormatReplaySource(pipelineSnapshot),
                pipelineSnapshot.Replay.PacketsReplayed,
                realDiagnostics.Options.DirectControlEnabled,
                realDiagnostics.Options.DirectControlArmed,
                realDiagnostics.Options.Selector.IsSelected,
                mockGear.Options.IsEnabled,
                mockPedalEffects.Options.IsEnabled,
                _realRoadVibrationOptions.IsEnabled,
                _realSlipLockOptions.IsEnabled),
            Path.GetFileName(HapticProfileStore.GetDefaultProfilePath()),
            Path.GetFileName(PhprEffectProfileStore.GetDefaultProfilePath()),
            _phprSoftwareCoexistenceSnapshot.Status.ToString(),
            realDiagnostics.Output.IsEmergencyStopActive,
            _phprManualValidationReadiness.IsBlocked,
            FormatRealPhprPulse(realDiagnostics.Options.BrakeGearPulse),
            FormatRealPhprPulse(realDiagnostics.Options.ThrottleGearPulse),
            BuildRealPhprGearPulseLatencyText(),
            _realRoadVibrationOptions.IsEnabled,
            FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Brake),
            FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Throttle),
            BuildRealRoadVibrationRoutingText(),
            _realSlipLockOptions.IsEnabled,
            FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip),
            FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock),
            BuildRealSlipLockRoutingText(),
            mockGear.Options.TargetModule.ToString(),
            mockGear.OutputSnapshot.AcceptedCommandCount,
            mockGear.OutputSnapshot.PendingScheduledStopCount,
            realDiagnostics.ReportWriteCount,
            realDiagnostics.FailedReportWriteCount,
            realDiagnostics.Connection.State.ToString(),
            realDiagnostics.LastError ?? "none",
            liveValidationSnapshot));
        return PhprWorkflowStatusPresenter.Build(snapshot);
    }

    private PhprLiveF1ValidationSnapshot BuildPhprLiveF1ValidationSnapshot(
        HapticPipelineSnapshot pipelineSnapshot,
        PHprRealOutputDiagnostics realDiagnostics)
    {
        var receiverSnapshot = _telemetryReceiver.GetSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        var paddle = _paddleInputSource.GetPaddleSnapshot();
        var shiftIntent = _shiftIntentProcessor.GetDiagnosticsSnapshot();

        return new PhprLiveF1ValidationSnapshot(
            TelemetryInputSource: pipelineSnapshot.InputSource.ToString(),
            PipelineRunning: pipelineSnapshot.IsRunning,
            UdpReceiverRunning: receiverSnapshot.IsRunning,
            UdpPacketCount: receiverSnapshot.PacketCount,
            ParserSuccessCount: pipelineSnapshot.ParserSuccessCount,
            TelemetryAge: pipelineSnapshot.TelemetryAge,
            TelemetryTimedOutMuted: pipelineSnapshot.TelemetryTimedOutMuted,
            DrivingArmed: driving.Current.IsArmed,
            DrivingArmedReason: driving.Current.Reason,
            PaddleListenerStatus: paddle.Status.ToString(),
            ShiftIntentEnabled: shiftIntent.IsEnabled,
            AcceptedShiftIntentCount: shiftIntent.AcceptedShiftIntentCount,
            SuppressedShiftIntentCount: shiftIntent.SuppressedShiftIntentCount,
            OutputMode: GetPhprWorkflowModeText(),
            MockGearRoutingEnabled: _mockGearPulseRouter.GetSnapshot().Options.IsEnabled,
            DirectControlEnabled: realDiagnostics.Options.DirectControlEnabled,
            DirectControlArmed: realDiagnostics.Options.DirectControlArmed,
            SelectedOutputConfigured: realDiagnostics.Options.Selector.IsSelected,
            CoexistenceStatus: _phprSoftwareCoexistenceSnapshot.Status.ToString(),
            EmergencyStopActive: realDiagnostics.Output.IsEmergencyStopActive,
            RealRoadVibrationEnabled: _realRoadVibrationOptions.IsEnabled,
            RealSlipLockEnabled: _realSlipLockOptions.IsEnabled);
    }

    private string GetPhprWorkflowModeText()
    {
        if (_realPhprOptions.DirectControlEnabled)
        {
            return "Real Direct Control";
        }

        var mockGearEnabled = _mockGearPulseRouter.GetSnapshot().Options.IsEnabled;
        var mockPedalEffectsEnabled = _mockPedalEffectsRouter.GetSnapshot().Options.IsEnabled;
        return mockGearEnabled || mockPedalEffectsEnabled ? "Mock" : "Disabled";
    }

    private void UpdatePhprSoftwareCoexistenceStatus()
    {
        var snapshot = _phprSoftwareCoexistenceSnapshot;
        PhprCoexistenceStatusText.Text =
            $"P-HPR coexistence: {snapshot.Status}; SimPro Manager {FormatBoolUnknown(snapshot.SimProRunning, snapshot.Status == PHprSoftwareConflictStatus.Unknown)}; SimHub {FormatBoolUnknown(snapshot.SimHubRunning, snapshot.Status == PHprSoftwareConflictStatus.Unknown)}; last scan {FormatScanTime(snapshot.LastScanAtUtc)}.";
        PhprCoexistenceItemsControl.ItemsSource = new[]
        {
            "Safety: read-only process detection only. Haptic Drive ASIO does not kill, hook, inject into, patch, control, or modify SimPro Manager or SimHub.",
            snapshot.Message,
            $"Direct-control safety status: {(snapshot.Status == PHprSoftwareConflictStatus.Clear ? "clear" : "blocked until clear")}. Direct control never starts output on launch and blocks non-clear coexistence.",
            $"Detected SimPro processes: {FormatDetectedSoftwareProcesses(snapshot.SimProProcesses)}",
            $"Detected SimHub processes: {FormatDetectedSoftwareProcesses(snapshot.SimHubProcesses)}",
            $"Detection supported: {snapshot.IsSupported}; error {snapshot.ErrorMessage ?? "none"}"
        };
    }

    private void UpdatePhprControlledWriteReadinessStatus()
    {
        _phprControlledWriteReadiness = PHprControlledWriteReadiness.Evaluate(BuildStage2PControlledWriteChecklist());
        PhprControlledWriteReadinessStatusText.Text =
            $"P-HPR direct write readiness: disabled; can enable {_phprControlledWriteReadiness.CanEnableDirectControl}; can arm {_phprControlledWriteReadiness.CanArmDirectControl}; can pulse {_phprControlledWriteReadiness.CanSendManualPulse}; issues {_phprControlledWriteReadiness.Issues.Count:N0}.";
        PhprControlledWriteReadinessItemsControl.ItemsSource = new[]
        {
            _phprControlledWriteReadiness.Status,
            "Safety: this Stage 2P readiness model remains no-write evidence. Stage 2Q direct controls are shown separately below and stay disabled/unarmed unless explicitly set for the current session.",
            "Manual gate: device/interface/report selection, explicit direct-control enable, explicit arming, visible emergency stop, clear SimPro/SimHub status, and user-supervised low-strength one-pulse test.",
            $"Blocking issues: {FormatReadinessIssues(_phprControlledWriteReadiness.Issues)}"
        };
    }

    private void UpdateRealPhprDirectControlStatus()
    {
        if (BeginInvokeOnUiIfRequired(UpdateRealPhprDirectControlStatus))
        {
            return;
        }

        var diagnostics = _realPhprOutput.GetDiagnostics();
        var options = diagnostics.Options;
        var selector = options.Selector;
        var coexistenceClear = _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Clear;
        var authorization = _phprWriteAuthorization.Current;
        var canPulse = options.DirectControlEnabled
            && options.DirectControlArmed
            && !options.CandidateIsRawInputOnly
            && options.CandidateHasOpenableHidPath
            && options.OpenCheckSucceeded
            && options.AllowsDirectPulseReportShape
            && selector.IsSelected
            && coexistenceClear
            && _outputInterlock.Current.AllowsOutput
            && authorization.IsAuthorized
            && !diagnostics.Output.IsEmergencyStopActive;
        TestRealPhprBrakePulseButton.IsEnabled = canPulse && options.BrakeGearPulse.IsEnabled;
        TestRealPhprThrottlePulseButton.IsEnabled = canPulse && options.ThrottleGearPulse.IsEnabled;
        RealPhprAuthorizationStatusText.Text = authorization.IsAuthorized
            ? $"Session authorization: authorized at {authorization.AuthorizedAtUtc:O}."
            : $"Session authorization: unauthorized. {authorization.Reason}.";
        RealPhprDirectStatusText.Text =
            $"Real direct control: {(options.DirectControlEnabled ? "enabled" : "disabled")}; arm {(options.DirectControlArmed ? "armed" : "not armed")}; authorization {(authorization.IsAuthorized ? "authorized" : "unauthorized")}; {(canPulse ? "Direct ready" : "Direct blocked")}; device {(selector.IsSelected ? "selected" : "not selected")}; road {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; connection {diagnostics.Connection.State}; interlock {_outputInterlock.Current.AllowsOutput}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}; emergency stop {diagnostics.Output.IsEmergencyStopActive}; report writes {diagnostics.ReportWriteCount:N0}; failures {diagnostics.FailedReportWriteCount:N0}.";
        RealPhprDirectItemsControl.ItemsSource = new[]
        {
            "Safety: write-capable direct path. Direct enable, arm state, selected private device path, session authorization, emergency stop, and write history are runtime-only and are not persisted.",
            $"Selected interface: {selector.InterfaceName}; transport {selector.Transport}; report ID {FormatReportId(selector.ReportId)}; report length {selector.ReportLength:N0} byte(s); expected first bytes {PHprHidReportShapeValidationResult.ExpectedF1EcStartFirstBytes}; private path {(selector.IsSelected ? "held in memory only" : "none")}.",
            $"Direct-output candidate picker: {_realPhprCandidateItems.Count:N0} refreshed candidate(s); source {options.CandidateSourceMethod}; raw-input-only {options.CandidateIsRawInputOnly}; openable HID path {options.CandidateHasOpenableHidPath}; output report known {options.CandidateOutputReportCapabilityKnown}; feature report known {options.CandidateFeatureReportCapabilityKnown}; report-shape attempted {options.ReportShapeValidationAttempted}; succeeded {options.ReportShapeValidationSucceeded}; failed {options.ReportShapeValidationFailed}; message {options.ReportShapeValidationMessage ?? "none"}; open-check attempted {options.OpenCheckAttempted}; succeeded {options.OpenCheckSucceeded}; failed {options.OpenCheckFailed}; sanitized open error {options.OpenCheckSanitizedErrorCategory ?? "none"}.",
            $"Session authorization: {(authorization.IsAuthorized ? "authorized" : "unauthorized")}; reason {authorization.Reason}; generation {authorization.Generation:N0}; interlock {(_outputInterlock.Current.AllowsOutput ? "clear" : "latched")}.",
            $"Lifecycle: connection {diagnostics.Connection.State}; writer open {diagnostics.Connection.WriterOpen}; opens {diagnostics.Connection.OpenSuccessCount:N0}/{diagnostics.Connection.OpenAttemptCount:N0}; closes {diagnostics.Connection.CloseSuccessCount:N0}/{diagnostics.Connection.CloseAttemptCount:N0}; timeout {options.WriteTimeoutMs:N0} ms.",
            $"Brake pulse: {(options.BrakeGearPulse.IsEnabled ? "enabled" : "disabled")}; strength {PhprUiValueConverter.FormatPercent(options.BrakeGearPulse.Strength01)}%; frequency {PhprUiValueConverter.FormatFrequency(options.BrakeGearPulse.FrequencyHz)} Hz; duration {options.BrakeGearPulse.DurationMs} ms.",
            $"Throttle pulse: {(options.ThrottleGearPulse.IsEnabled ? "enabled" : "disabled")}; strength {PhprUiValueConverter.FormatPercent(options.ThrottleGearPulse.Strength01)}%; frequency {PhprUiValueConverter.FormatFrequency(options.ThrottleGearPulse.FrequencyHz)} Hz; duration {options.ThrottleGearPulse.DurationMs} ms.",
            $"Road vibration: {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; brake {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Brake)}; throttle {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Throttle)}; last {BuildRealRoadVibrationRoutingText()}.",
            $"Slip/lock: {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; slip {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip)}; lock {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock)}; details {BuildRealSlipLockDiagnosticsText()}.",
            $"Manual pulse buttons: {(canPulse ? "available" : "blocked")}; requires enabled and armed direct control, session authorization, clear output interlock, selected HID device-interface candidate, successful open-check, selected output/feature report capability, successful no-command report-shape validation, clear coexistence, and no emergency stop latch.",
            $"Last command: {FormatPhprCommand(diagnostics.Output.LastCommand)}; last status {diagnostics.Output.LastStatus?.ToString() ?? "none"}; message {diagnostics.Output.LastMessage ?? "none"}.",
            $"Last gear-pulse latency: {BuildRealPhprGearPulseLatencyText()}.",
            $"Last HID report: {diagnostics.LastReportState?.ToString() ?? "none"}; target {diagnostics.LastTarget?.ToString() ?? "none"}; length {diagnostics.LastReportLength:N0}; summary {diagnostics.LastReportSummary ?? "none"}; write {diagnostics.Connection.LastWriteStatus?.ToString() ?? "none"}; stop {diagnostics.Connection.LastStopStatus?.ToString() ?? "none"}; error {diagnostics.LastError ?? "none"}.",
            $"Failure counters: disconnects {diagnostics.Connection.DisconnectCount:N0}; timeouts {diagnostics.Connection.TimeoutCount:N0}; invalid reports {diagnostics.Connection.InvalidReportCount:N0}; stop reports {diagnostics.Connection.StopReportWriteCount:N0}."
        };
    }

    private void UpdatePhprValidationStatus()
    {
        if (BeginInvokeOnUiIfRequired(UpdatePhprValidationStatus))
        {
            return;
        }

        var checklist = BuildPhprManualValidationChecklist();
        _phprManualValidationReadiness = PHprManualValidationReadiness.Evaluate(checklist);
        var result = BuildPhprManualValidationResult();
        var evaluation = result.Evaluate();
        PhprValidationStatusText.Text =
            $"P-HPR validation harness: {(_phprManualValidationReadiness.IsBlocked ? "blocked" : "ready")}; brake pulse {_phprManualValidationReadiness.CanRunBrakePulse}; throttle pulse {_phprManualValidationReadiness.CanRunThrottlePulse}; gear paddle {_phprManualValidationReadiness.CanRunGearPaddleTest}; result {(evaluation.CanMarkPass ? "pass-ready" : evaluation.PassRequested ? "pass blocked" : "draft/non-pass")}.";
        PhprValidationItemsControl.ItemsSource = new[]
        {
            "Safety: checklist/export only. This harness does not trigger brake, throttle, or paddle test pulses.",
            _phprManualValidationReadiness.Status,
            $"Checklist issues: {FormatPhprValidationIssues(_phprManualValidationReadiness.Issues)}",
            $"Result issues: {FormatPhprValidationIssues(evaluation.Issues)}",
            $"Selected real output: {(_realPhprOptions.Selector.IsSelected ? "configured for this session" : "not selected")}; direct control {(_realPhprOptions.DirectControlEnabled ? "enabled" : "disabled")}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}.",
            $"Manual confirmations: user {checklist.UserPhysicallyPresent}; P700 {checklist.P700Connected}; brake module {checklist.BrakeModuleInstalled}; throttle module {checklist.ThrottleModuleInstalled}; gear paddle planned {checklist.GearPaddleTestPlanned}.",
            $"Last export: {_lastPhprValidationExportPath ?? "none"}; default export folder {GetLocalValidationResultsDirectory()}."
        };
    }

    private void UpdateDiagnosticsStatus()
    {
        if (BeginInvokeOnUiIfRequired(UpdateDiagnosticsStatus))
        {
            return;
        }

        if (DiagnosticsPanel.Visibility != Visibility.Visible
            && NavigationList.SelectedItem is not ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            return;
        }

        ApplyDiagnosticsStatusPresentation(BuildDiagnosticsStatusPresentation());
    }

    private DiagnosticsStatusPresentation BuildDiagnosticsStatusPresentation()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var outputStatus = pipelineSnapshot.Output;
        var effectSnapshot = pipelineSnapshot.Effects;
        var testBenchSnapshot = _testBench.GetSnapshot();
        var audioDiagnostics = AudioRuntimeDiagnosticsSnapshot.Create(
            outputStatus,
            effectSnapshot,
            pipelineSnapshot.Audio,
            testBenchSnapshot);
        var receiverSnapshot = _telemetryReceiver.GetSnapshot();
        var ingressSnapshot = _telemetryIngressWorker.GetSnapshot();
        var forwarderSnapshot = pipelineSnapshot.Forwarding;
        var recordingSnapshot = pipelineSnapshot.Recording;
        var replaySnapshot = pipelineSnapshot.Replay;
        var parserSuccess = pipelineSnapshot.ParserSuccessCount;
        var parserIgnored = pipelineSnapshot.ParserIgnoredCount;
        var parserFailed = pipelineSnapshot.ParserFailureCount;
        var vehicleUpdates = pipelineSnapshot.VehicleStateUpdateCount;
        var packetDiagnostics = BuildPacketDiagnosticsText(pipelineSnapshot.PacketDiagnostics);
        var roadDiagnostics = BuildRoadTextureDiagnosticsSnapshot(
            pipelineSnapshot,
            audioDiagnostics,
            _roadTextureFlightRecorder.IsEnabled,
            _roadTextureFlightRecorder.LogPath);
        var roadDiagnosticLines = roadDiagnostics.ToDiagnosticsLines();
        var realDiagnostics = _realPhprOutput.GetDiagnostics();
        var phprWorkflowPresentation = BuildPhprWorkflowStatusPresentation(pipelineSnapshot, realDiagnostics);
        var bst1Diagnostics = Bst1DiagnosticsSectionBuilder.Build(new Bst1DiagnosticsSectionInputs(
            EffectSnapshot: effectSnapshot,
            MixerPeakLevel: audioDiagnostics.MixerPeakLevel,
            OutputPeakLevel: audioDiagnostics.OutputPeakLevel,
            LimitedSampleCount: audioDiagnostics.LimitedSampleCount,
            ClippedSampleCount: audioDiagnostics.ClippedSampleCount,
            EmergencyMute: audioDiagnostics.EmergencyMute));
        var snapshot = DiagnosticsStatusSnapshotBuilder.Build(new DiagnosticsStatusBuildInputs(
            GeneratedAt: DateTimeOffset.Now,
            FlightRecorderActive: roadDiagnostics.FlightRecorderActive,
            FlightRecorderPath: roadDiagnostics.FlightRecorderPath,
            FlightRecorderLastFallbackStatus: _roadTextureFlightRecorder.LastFallbackStatus ?? "none",
            UdpPacketCount: receiverSnapshot.PacketCount,
            ParserSuccessCount: parserSuccess,
            ParserFailureCount: parserFailed,
            ActiveEffectCount: audioDiagnostics.ActiveEffectCount,
            OutputPeakLevel: audioDiagnostics.OutputPeakLevel,
            RenderCallbackCount: outputStatus.RenderCallbackCount,
            PipelineText: $"{(pipelineSnapshot.IsRunning ? "running" : "stopped")}; source {pipelineSnapshot.InputSource}; rendered {pipelineSnapshot.RenderedBufferCount:N0} buffer(s); telemetry age {(pipelineSnapshot.TelemetryAge is null ? "none" : $"{pipelineSnapshot.TelemetryAge.Value.TotalMilliseconds:0} ms")}; stale mute {pipelineSnapshot.TelemetryTimedOutMuted}; skipped telemetry ticks {Interlocked.Read(ref _telemetryStatusTickSkippedCount):N0}; last error {pipelineSnapshot.LastPipelineError ?? "none"}.",
            UdpListenerText: $"{(receiverSnapshot.IsRunning ? "running" : "stopped")} on port {receiverSnapshot.BoundPort}; bind {BuildTelemetryBindAddressText()}; allow LAN {_allowLanTelemetry}; allowlist {_allowedTelemetryRemoteAddresses.Count:N0}; received {ingressSnapshot.ReceivedPacketCount:N0}; rate {receiverSnapshot.PacketRatePerSecond:0.00}/s; ignored remotes {receiverSnapshot.IgnoredRemotePacketCount:N0}; oversized {receiverSnapshot.OversizedDatagramCount:N0}; last packet {(receiverSnapshot.LastPacketAtUtc is null ? "never" : $"{receiverSnapshot.TimeSinceLastPacket?.TotalSeconds:0.0}s ago")}; warning {_telemetryListenerWarning ?? "none"}.",
            UdpForwardingText: $"{forwarderSnapshot.EnabledDestinationCount}/{forwarderSnapshot.DestinationCount} destination(s) enabled; {forwarderSnapshot.ForwardedDatagramCount:N0} datagrams; ingress dropped {ingressSnapshot.ForwardingDroppedPacketCount:N0}; {forwarderSnapshot.ErrorCount:N0} error(s).",
            UdpForwardingDestinationsText: BuildForwardingDestinationsText(),
            ParserText: $"{parserSuccess:N0} valid, {parserIgnored:N0} ignored, {parserFailed:N0} failed. {pipelineSnapshot.LastPacketMessage}",
            PacketIdsText: packetDiagnostics,
            VehicleStateText: $"{vehicleUpdates:N0} update(s). {pipelineSnapshot.LastVehicleStateMessage}",
            RecordingText: $"{(recordingSnapshot.IsRecording ? "active" : "inactive")}; {recordingSnapshot.PacketCount:N0} packet(s); file {(recordingSnapshot.FilePath is null ? "none" : Path.GetFileName(recordingSnapshot.FilePath))}; ingress dropped {ingressSnapshot.RecordingDroppedPacketCount:N0}; queued dropped {recordingSnapshot.DroppedPacketCount:N0}; incomplete {(recordingSnapshot.RecordingIncomplete || ingressSnapshot.RecordingMarkedIncomplete)}.",
            ReplayText: $"{(replaySnapshot.IsReplaying ? "active" : "inactive")}; source {FormatReplaySource(pipelineSnapshot)}; {replaySnapshot.PacketsReplayed:N0} packet(s); {replaySnapshot.StatusMessage}",
            Effects: bst1Diagnostics.Effects,
            Bst1SlipLockText: bst1Diagnostics.SlipLockText,
            MixerSafetyText: bst1Diagnostics.MixerSafetyText,
            RoadDiagnosticsLines: roadDiagnosticLines,
            PhprSlipLockText: BuildRealSlipLockDiagnosticsText(),
            TestBenchText: $"{(testBenchSnapshot.IsActive ? "active" : "inactive")}; signal {testBenchSnapshot.SelectedSignalName}; output {testBenchSnapshot.OutputDisplayName}; peak {testBenchSnapshot.OutputPeakLevel:0.000}.",
            OutputText: $"{outputStatus.DisplayName} ({outputStatus.State}); streaming {outputStatus.IsStreaming}; hardware required {outputStatus.RequiresPhysicalHardware}; manual debug {outputStatus.IsManualDebugOnly}; hardware-absent mode {audioDiagnostics.HardwareAbsentMode}; null buffers {pipelineSnapshot.NullOutput?.SubmittedBufferCount ?? 0:N0}; render callbacks {outputStatus.RenderCallbackCount:N0}; backend callbacks {outputStatus.BackendCallbackCount:N0}; output buffers {outputStatus.SubmittedBufferCount:N0}; drops {outputStatus.DroppedBufferCount:N0}; underruns {outputStatus.UnderrunCount:N0}; render {FormatDuration(outputStatus.LastRenderDuration)}; jitter {FormatDuration(outputStatus.LastCallbackJitter)}.",
            InputDiscoveryText: BuildInputDiscoveryDiagnosticsText(),
            PaddleInputListenerText: BuildPaddleInputDiagnosticsText(),
            ShiftIntentText: BuildShiftIntentDiagnosticsText(),
            ProfilePersistenceText: phprWorkflowPresentation.ProfilePersistenceDiagnosticsLine,
            WorkflowText: phprWorkflowPresentation.WorkflowDiagnosticsLine,
            LiveValidationText: phprWorkflowPresentation.LiveValidationDiagnosticsLine,
            PhprSoftwareCoexistenceText: BuildPhprCoexistenceDiagnosticsText(),
            PhprDirectWriteReadinessText: BuildPhprControlledWriteReadinessDiagnosticsText(),
            PhprRealDirectControlText: BuildRealPhprDirectDiagnosticsText(),
            PhprValidationHarnessText: BuildPhprValidationDiagnosticsText(),
            PaddleGearBenchText: BuildPaddleGearBenchDiagnosticsText(),
            MockGearRoutingText: BuildMockGearPulseDiagnosticsText(),
            MockPedalEffectsText: BuildMockPedalEffectsDiagnosticsText(),
            ManualAsioHardwareTestText: BuildManualAsioHardwareTestDiagnosticsText(),
            AsioReadinessText: $"{_asioReadinessSnapshot.Message} Drivers reported {_asioReadinessSnapshot.DriverNames.Count}; M-Audio match {(_asioReadinessSnapshot.MTrackDriverVisible ? "yes" : "no")}; channel {(_asioReadinessSnapshot.SelectedOutputChannel is null ? "none" : _asioReadinessSnapshot.SelectedOutputChannel)}; armed {_asioReadinessSnapshot.IsArmed}; Windows sound output proves ASIO {_asioReadinessSnapshot.WindowsSoundOutputVisibilityProvesAsio}.",
            RuntimePrerequisitesText: $".NET {Environment.Version}; WPF desktop runtime is present because the app is running; launch script sets DOTNET_ROOT to the repo-local runtime before starting the executable.",
            AppSettingsText: BuildPersistedSettingsStatusPresentation().DiagnosticsText));
        return DiagnosticsStatusPresenter.Build(snapshot);
    }

    private SupportBundleStructuredDiagnostics BuildSupportBundleStructuredDiagnostics(DateTimeOffset generatedAtUtc)
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var outputStatus = pipelineSnapshot.Output;
        var effectSnapshot = pipelineSnapshot.Effects;
        var testBenchSnapshot = _testBench.GetSnapshot();
        var audioDiagnostics = AudioRuntimeDiagnosticsSnapshot.Create(
            outputStatus,
            effectSnapshot,
            pipelineSnapshot.Audio,
            testBenchSnapshot);
        var receiverSnapshot = _telemetryReceiver.GetSnapshot();
        var ingressSnapshot = _telemetryIngressWorker.GetSnapshot();
        var correlationIds = CaptureSupportBundleCorrelationIds(pipelineSnapshot);

        return StructuredDiagnosticsBuilder.Build(
            new StructuredDiagnosticsBuildInputs(
                generatedAtUtc,
                GameTelemetryCatalog.NormalizeGameId(_selectedGameId),
                GameTelemetryCatalog.GetDisplayName(_selectedGameId),
                BuildSelectedOutputId(),
                _currentProfile.Name,
                _settingsError,
                pipelineSnapshot,
                receiverSnapshot,
                ingressSnapshot,
                audioDiagnostics,
                correlationIds));
    }

    private void ApplyDiagnosticsStatusPresentation(DiagnosticsStatusPresentation presentation)
    {
        _diagnosticsPresentationController.Publish(
            presentation.SummaryText,
            string.Join(Environment.NewLine, presentation.Items));
        AdvancedDiagnosticsViewControl.Apply(presentation);

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            PageStatusText.Text = presentation.SummaryText;
        }
    }

    private static string BuildPacketDiagnosticsText(IReadOnlyList<HapticPipelinePacketDiagnostics> diagnostics)
    {
        var observed = diagnostics
            .Where(item => item.ObservedCount > 0)
            .Select(item => $"{item.Name}#{item.PacketId}: {item.ObservedCount:N0}")
            .ToArray();

        return observed.Length == 0
            ? "no packet IDs observed yet"
            : string.Join("; ", observed);
    }

    private RoadTextureDiagnosticSnapshot BuildRoadTextureDiagnosticsSnapshot(
        HapticPipelineSnapshot pipelineSnapshot,
        AudioRuntimeDiagnosticsSnapshot audioDiagnostics,
        bool flightRecorderActive,
        string flightRecorderPath)
    {
        var continuousRuntime = _realPhprContinuousEffectsRuntime.GetSnapshot();
        return RoadTextureDiagnosticSnapshot.Create(
            pipelineSnapshot,
            audioDiagnostics,
            _currentProfile,
            _realRoadVibrationOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits),
            _realRoadVibrationRouter.GetSnapshot(),
            _realPhprOutput.GetDiagnostics(),
            continuousRuntime.RoadHigherPrioritySuppressedCount,
            continuousRuntime.RoadInFlightSuppressedCount,
            flightRecorderActive,
            flightRecorderPath);
    }

    private void RecordRoadTextureFlightRecorder(HapticPipelineSnapshot pipelineSnapshot)
    {
        var recorder = _roadTextureFlightRecorder;
        if (!recorder.IsEnabled)
        {
            return;
        }

        var audioDiagnostics = AudioRuntimeDiagnosticsSnapshot.Create(
            pipelineSnapshot.Output,
            pipelineSnapshot.Effects,
            pipelineSnapshot.Audio,
            _testBench.GetSnapshot());
        var roadDiagnostics = BuildRoadTextureDiagnosticsSnapshot(
            pipelineSnapshot,
            audioDiagnostics,
            recorder.IsEnabled,
            recorder.LogPath);
        recorder.Record(RoadTextureFlightRecord.From(_roadTextureFlightRecorderSessionId, roadDiagnostics));
    }

    private string BuildInputDiscoveryDiagnosticsText()
    {
        var snapshot = _inputDiscoverySnapshot;
        if (!snapshot.HasRun)
        {
            return "not refreshed; manual Refresh Input Devices is available on Devices; no listener, mapping, routing, or output is active.";
        }

        var errors = snapshot.Errors.Count == 0 ? "none" : string.Join("; ", snapshot.Errors);
        return $"{snapshot.DeviceCount:N0} device(s); methods {FormatDiscoveryMethods(snapshot.Methods)}; wheelbase {snapshot.LikelySimagicWheelBaseCandidates.Count:N0}; GT Neo/wheel {snapshot.LikelyGtNeoWheelInputCandidates.Count:N0}; P700 {snapshot.LikelyP700PedalCandidates.Count:N0}; unknown HID/game-controller {snapshot.UnknownHidOrGameControllerCandidates.Count:N0}; errors {errors}; read-only discovery with Stage 2E Windows game-controller listener and Stage 2F shift intent diagnostics available separately.";
    }

    private static string FormatReplaySource(HapticPipelineSnapshot snapshot)
    {
        if (snapshot.InputSource == HapticPipelineInputSource.Replay)
        {
            return string.IsNullOrWhiteSpace(snapshot.Replay.SourceFilePath)
                ? "in-memory replay"
                : Path.GetFileName(snapshot.Replay.SourceFilePath);
        }

        return string.IsNullOrWhiteSpace(snapshot.Replay.SourceFilePath)
            ? "none"
            : Path.GetFileName(snapshot.Replay.SourceFilePath);
    }

    private string BuildPaddleInputDiagnosticsText()
    {
        var snapshot = _paddleInputSource.GetPaddleSnapshot();
        var selected = snapshot.SelectedDevice is null
            ? _paddleMapping.SelectedDeviceId ?? "none"
            : $"{snapshot.SelectedDevice.DisplayName} ({snapshot.SelectedDevice.DeviceId})";
        var lastRaw = snapshot.LastChangedButtonId is null
            ? "none"
            : $"button {snapshot.LastChangedButtonId} {snapshot.LastChangedButtonState}";
        var lastMapped = snapshot.LastPaddleEvent is null
            ? "none"
            : $"{snapshot.LastPaddleEvent.PaddleSide} button {snapshot.LastPaddleEvent.ButtonId} utc {snapshot.LastPaddleEvent.TimestampUtc:O} ticks {snapshot.LastPaddleEvent.StopwatchTicks}";

        return $"{snapshot.Status}; selected {selected}; method {_paddleMapping.SelectedMethod}; left {FormatButtonMapping(snapshot.Mapping.LeftPaddleButtonId)} state {snapshot.LeftPaddleState}; right {FormatButtonMapping(snapshot.Mapping.RightPaddleButtonId)} state {snapshot.RightPaddleState}; last raw {lastRaw}; last mapped {lastMapped}; count {snapshot.PaddlePressCount:N0}; debounce {snapshot.Mapping.DebounceDuration.TotalMilliseconds:0} ms; debounce suppressed {snapshot.DebounceSuppressedCount:N0}; error {snapshot.LastErrorMessage ?? "none"}; diagnostics only, no haptic output.";
    }

    private string BuildShiftIntentDiagnosticsText()
    {
        var diagnostics = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        var lastAccepted = diagnostics.LastAcceptedEvent is null
            ? "none"
            : $"{diagnostics.LastAcceptedEvent.Direction} seq {diagnostics.LastAcceptedEvent.SequenceNumber} utc {diagnostics.LastAcceptedEvent.TimestampUtc:O} button {diagnostics.LastAcceptedEvent.SourceButtonId?.ToString() ?? "unknown"} gear {FormatOptionalInt(diagnostics.LastAcceptedEvent.LastTelemetryGear)}";
        var lastSuppressed = diagnostics.LastSuppressedEvent is null
            ? "none"
            : $"{diagnostics.LastSuppressedEvent.PaddleEvent.PaddleSide} seq {diagnostics.LastSuppressedEvent.PaddleEvent.SequenceNumber} reason {diagnostics.LastSuppressedEvent.SuppressionReason}";

        return $"{(diagnostics.IsEnabled ? "enabled" : "disabled")}; mode {diagnostics.Mode}; DrivingArmed {driving.Current.IsArmed} reason {driving.Current.Reason}; menu safe {driving.MenuSafeModeEnabled}; require recent telemetry {driving.RequireRecentTelemetry}; accepted {diagnostics.AcceptedShiftIntentCount:N0}; suppressed {diagnostics.SuppressedShiftIntentCount:N0}; last accepted {lastAccepted}; last suppressed {lastSuppressed}; pending confirmations {diagnostics.PendingConfirmationCount:N0}; accepted events may feed mock-only P-HPR gear routing.";
    }

    private string BuildPhprCoexistenceDiagnosticsText()
    {
        var snapshot = _phprSoftwareCoexistenceSnapshot;
        return $"status {snapshot.Status}; SimPro {FormatBoolUnknown(snapshot.SimProRunning, snapshot.Status == PHprSoftwareConflictStatus.Unknown)}; SimHub {FormatBoolUnknown(snapshot.SimHubRunning, snapshot.Status == PHprSoftwareConflictStatus.Unknown)}; last scan {FormatScanTime(snapshot.LastScanAtUtc)}; supported {snapshot.IsSupported}; direct control blocked {snapshot.Status != PHprSoftwareConflictStatus.Clear}; read-only process detection only; error {snapshot.ErrorMessage ?? "none"}.";
    }

    private string BuildPhprControlledWriteReadinessDiagnosticsText()
    {
        var readiness = _phprControlledWriteReadiness;
        return $"{readiness.Status}; no-write stage {readiness.IsNoWriteStage}; enable {readiness.CanEnableDirectControl}; arm {readiness.CanArmDirectControl}; manual pulse {readiness.CanSendManualPulse}; issues {FormatReadinessIssues(readiness.Issues)}";
    }

    private string BuildRealPhprDirectDiagnosticsText()
    {
        var diagnostics = _realPhprOutput.GetDiagnostics();
        var options = diagnostics.Options;
        var selector = options.Selector;
        var canPulse = options.DirectControlEnabled
            && !options.CandidateIsRawInputOnly
            && options.CandidateHasOpenableHidPath
            && options.OpenCheckSucceeded
            && options.AllowsDirectPulseReportShape
            && selector.IsSelected
            && _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Clear
            && !diagnostics.Output.IsEmergencyStopActive;
        return $"{(options.DirectControlEnabled ? "enabled" : "disabled")}; selected {selector.IsSelected}; candidates {_realPhprCandidateItems.Count:N0}; source {options.CandidateSourceMethod}; raw-input-only {options.CandidateIsRawInputOnly}; openable {options.CandidateHasOpenableHidPath}; transport {selector.Transport}; output report known {options.CandidateOutputReportCapabilityKnown}; feature report known {options.CandidateFeatureReportCapabilityKnown}; report-shape attempted {options.ReportShapeValidationAttempted}; succeeded {options.ReportShapeValidationSucceeded}; failed {options.ReportShapeValidationFailed}; shape message {options.ReportShapeValidationMessage ?? "none"}; expected first bytes {PHprHidReportShapeValidationResult.ExpectedF1EcStartFirstBytes}; open-check attempted {options.OpenCheckAttempted}; succeeded {options.OpenCheckSucceeded}; failed {options.OpenCheckFailed}; open error {options.OpenCheckSanitizedErrorCategory ?? "none"}; connection {diagnostics.Connection.State}; writer open {diagnostics.Connection.WriterOpen}; interface {selector.InterfaceName}; report ID {FormatReportId(selector.ReportId)}; report length {selector.ReportLength:N0}; private path {(selector.IsSelected ? "held in memory only" : "none")}; timeout {options.WriteTimeoutMs:N0} ms; can pulse {canPulse}; brake {FormatRealPhprPulse(options.BrakeGearPulse)}; throttle {FormatRealPhprPulse(options.ThrottleGearPulse)}; road {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")} brake {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Brake)} throttle {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Throttle)} last road {BuildRealRoadVibrationRoutingText()}; slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")} slip {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip)} lock {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock)} last slip/lock {BuildRealSlipLockDiagnosticsText()}; gear latency {BuildRealPhprGearPulseLatencyText()}; writes {diagnostics.ReportWriteCount:N0}; failures {diagnostics.FailedReportWriteCount:N0}; opens {diagnostics.Connection.OpenSuccessCount:N0}/{diagnostics.Connection.OpenAttemptCount:N0}; closes {diagnostics.Connection.CloseSuccessCount:N0}/{diagnostics.Connection.CloseAttemptCount:N0}; stops {diagnostics.Connection.StopReportWriteCount:N0}; disconnects {diagnostics.Connection.DisconnectCount:N0}; timeouts {diagnostics.Connection.TimeoutCount:N0}; invalid reports {diagnostics.Connection.InvalidReportCount:N0}; last target {diagnostics.LastTarget?.ToString() ?? "none"}; last report {diagnostics.LastReportState?.ToString() ?? "none"} {diagnostics.LastReportLength:N0} bytes; last status {diagnostics.Output.LastStatus?.ToString() ?? "none"}; write {diagnostics.Connection.LastWriteStatus?.ToString() ?? "none"}; stop {diagnostics.Connection.LastStopStatus?.ToString() ?? "none"}; open {diagnostics.Connection.LastOpenStatus?.ToString() ?? "none"}; close {diagnostics.Connection.LastCloseStatus?.ToString() ?? "none"}; last error {diagnostics.LastError ?? "none"}; runtime-only enable/open-check/device not persisted; safe gear-pulse, road, and slip/lock settings persisted.";
    }

    private string BuildRealRoadVibrationRoutingText()
    {
        var result = _realPhprContinuousEffectsRuntime.GetSnapshot().LastRoadVibrationRoutingResult;
        return result is null
            ? "none"
            : $"{result.Status}; routed {result.WasRouted}; intensity {result.Intensity01:0.###}; commands {result.Commands.Count:N0}; at {FormatTimestamp(result.RoutedAtUtc)}";
    }

    private string BuildRealSlipLockRoutingText()
    {
        var continuousRuntime = _realPhprContinuousEffectsRuntime.GetSnapshot();
        var snapshot = _realSlipLockRouter.GetSnapshot();
        if (snapshot.RouteAttemptCount <= 0 && snapshot.LastResult is null)
        {
            return "none";
        }

        var lastResult = snapshot.LastResult;
        return $"{lastResult?.Status.ToString() ?? "none"}; runtime {snapshot.RuntimeState}; active {snapshot.ActiveSlipLockModules}; cadence slip/lock {snapshot.Options.WheelSlip.TextureCadenceMs:0}/{snapshot.Options.WheelLock.TextureCadenceMs:0} ms; hold {snapshot.Options.HoldTimeout.TotalMilliseconds:0} ms; routed {snapshot.RouteCount:N0}; safety rejected {snapshot.SafetyRejectedCount:N0}; interval suppressed {snapshot.IntervalSuppressedCount:N0}; stale {snapshot.StaleTelemetrySuppressedCount:N0}; command-rate {snapshot.CommandRateSuppressedCount:N0}; stops {snapshot.StopCommandCount:N0}; gear protected {snapshot.GearProtectionSuppressedCount:N0}; road yield {continuousRuntime.RoadHigherPrioritySuppressedCount:N0}; last target {snapshot.LastTargetModule?.ToString() ?? "none"}; slip {FormatRealSlipLockEffectDiagnostics(snapshot.WheelSlip, includeTelemetry: false)}; lock {FormatRealSlipLockEffectDiagnostics(snapshot.WheelLock, includeTelemetry: false)}; at {FormatTimestamp(lastResult?.RoutedAtUtc ?? snapshot.LastRouteAttemptAtUtc)}";
    }

    private string BuildRealSlipLockDiagnosticsText()
    {
        var continuousRuntime = _realPhprContinuousEffectsRuntime.GetSnapshot();
        var snapshot = _realSlipLockRouter.GetSnapshot();
        if (snapshot.RouteAttemptCount <= 0 && snapshot.LastResult is null)
        {
            return "none";
        }

        return $"runtime {snapshot.RuntimeState}; active {snapshot.ActiveSlipLockModules}; cadence slip/lock {snapshot.Options.WheelSlip.TextureCadenceMs:0}/{snapshot.Options.WheelLock.TextureCadenceMs:0} ms; hold {snapshot.Options.HoldTimeout.TotalMilliseconds:0} ms; attempts {snapshot.RouteAttemptCount:N0}; routed {snapshot.RouteCount:N0}; safety rejected {snapshot.SafetyRejectedCount:N0}; interval suppressed {snapshot.IntervalSuppressedCount:N0}; stale {snapshot.StaleTelemetrySuppressedCount:N0}; command-rate {snapshot.CommandRateSuppressedCount:N0}; stops {snapshot.StopCommandCount:N0}; gear protected {snapshot.GearProtectionSuppressedCount:N0}; road yield {continuousRuntime.RoadHigherPrioritySuppressedCount:N0}; watchdog stops {snapshot.WatchdogStopCount:N0}; stop reason {snapshot.LastSlipLockStopReason}; last result {snapshot.LastResult?.Status.ToString() ?? "none"}; last target {snapshot.LastTargetModule?.ToString() ?? "none"}; start age {FormatTimestampAge(snapshot.LastSlipLockStartAtUtc)}; update age {FormatTimestampAge(snapshot.LastSlipLockUpdateAtUtc)}; stop age {FormatTimestampAge(snapshot.LastSlipLockStopAtUtc)}; slip {FormatRealSlipLockEffectDiagnostics(snapshot.WheelSlip, includeTelemetry: true)}; lock {FormatRealSlipLockEffectDiagnostics(snapshot.WheelLock, includeTelemetry: true)}";
    }

    private string BuildRealPhprGearPulseLatencyText()
    {
        var result = _lastRealPhprGearPulseRoutingResult;
        if (result is null)
        {
            return "none";
        }

        var paddleToAccepted = DurationBetween(result.PaddleEventAtUtc, result.ShiftIntentAcceptedAtUtc);
        var acceptedToCommand = DurationBetween(result.ShiftIntentAcceptedAtUtc, result.FirstCommandCreatedAtUtc);
        var commandToWrite = DurationBetween(result.FirstCommandCreatedAtUtc, result.FirstWriteCompletedAtUtc);
        var traceCount = result.CommandTraces?.Count ?? 0;
        return $"routed {result.Routed}; paddle {FormatTimestamp(result.PaddleEventAtUtc)}; accepted {FormatTimestamp(result.ShiftIntentAcceptedAtUtc)} ({FormatDuration(paddleToAccepted)}); command {FormatTimestamp(result.FirstCommandCreatedAtUtc)} ({FormatDuration(acceptedToCommand)}); write {FormatTimestamp(result.FirstWriteCompletedAtUtc)} ({FormatDuration(commandToWrite)}); traces {traceCount:N0}";
    }

    private string BuildPhprValidationDiagnosticsText()
    {
        var checklist = BuildPhprManualValidationChecklist();
        var result = BuildPhprManualValidationResult();
        var evaluation = result.Evaluate();
        return $"{_phprManualValidationReadiness.Status}; brake {_phprManualValidationReadiness.CanRunBrakePulse}; throttle {_phprManualValidationReadiness.CanRunThrottlePulse}; gear {_phprManualValidationReadiness.CanRunGearPaddleTest}; issues {FormatPhprValidationIssues(_phprManualValidationReadiness.Issues)}; pass requested {evaluation.PassRequested}; can mark pass {evaluation.CanMarkPass}; result issues {FormatPhprValidationIssues(evaluation.Issues)}; confirmations user {checklist.UserPhysicallyPresent}, P700 {checklist.P700Connected}, brake {checklist.BrakeModuleInstalled}, throttle {checklist.ThrottleModuleInstalled}; last export {_lastPhprValidationExportPath ?? "none"}; no hardware output triggered by harness.";
    }

    private string BuildPaddleGearBenchDiagnosticsText()
    {
        var snapshot = _paddleGearBenchTestController.GetSnapshot();
        ConfigurePhprDirectRuntime();
        var runtime = _phprDirectRuntime.GetSnapshot();
        var diagnostics = _realPhprOutput.GetDiagnostics();
        var lastSource = snapshot.LastPaddleEvent is null
            ? "none"
            : $"device {snapshot.LastPaddleEvent.SourceDevice?.DeviceId ?? "unknown"} event {snapshot.LastPaddleEvent.ButtonState} side {snapshot.LastPaddleEvent.PaddleSide} button {snapshot.LastPaddleEvent.ButtonId} seq {snapshot.LastPaddleEvent.SequenceNumber:N0}";
        var lastDecision = snapshot.LastResult is null
            ? "none"
            : snapshot.LastResult.Accepted
                ? $"accepted {snapshot.LastResult.Message}"
                : $"rejected {snapshot.LastResult.SuppressionReason ?? snapshot.LastResult.Message}";
        return $"{(snapshot.IsEnabled ? "enabled" : "disabled")}/{(snapshot.IsArmed ? "auto-armed" : "blocked")}; output {snapshot.OutputMode}; target {snapshot.Options.TargetModule}; runtime {runtime.State}; route service {(runtime.SharedPathProof.IsProven ? PhprDeviceCardPulseService.RouteName : "blocked")}; pulse id {runtime.PulseId:N0}; brake {FormatRealPhprPulse(_realPhprOptions.BrakeGearPulse)}; throttle {FormatRealPhprPulse(_realPhprOptions.ThrottleGearPulse)}; accepted {snapshot.AcceptedBenchGearEventCount:N0}; suppressed {snapshot.SuppressedBenchGearEventCount:N0}; left {snapshot.LeftPaddleAcceptedCount:N0}; right {snapshot.RightPaddleAcceptedCount:N0}; source {lastSource}; decision {lastDecision}; active pulse {diagnostics.ActivePulse}; pending stops {diagnostics.Output.PendingScheduledStopCount:N0}; generations brake {diagnostics.BrakePulseGeneration:N0} throttle {diagnostics.ThrottlePulseGeneration:N0}; retrigger {diagnostics.RetriggerCount:N0}; stale stop ignored {diagnostics.StaleStopIgnoredCount:N0}; stale output dropped {diagnostics.StaleOutputDroppedCount:N0}; marker {runtime.UncleanShutdownMarkerExists}; last start {FormatTimestamp(diagnostics.LastStartSentAtUtc)} target {diagnostics.LastStartReportTarget?.ToString() ?? "none"}; scheduled stop {FormatTimestamp(runtime.Latency.StopDueUtc ?? diagnostics.LastScheduledStopDueAtUtc)}; last stop {FormatTimestamp(runtime.Latency.StopWriteCompletedUtc ?? diagnostics.LastStopSentAtUtc)} target {diagnostics.LastStopReportTarget?.ToString() ?? "none"}; stop result {diagnostics.LastStopResultStatus?.ToString() ?? "none"} {diagnostics.LastStopResultMessage ?? "none"}; emergency stop {FormatTimestamp(diagnostics.LastEmergencyStopRequestedAtUtc)} {diagnostics.LastEmergencyStopResultStatus?.ToString() ?? "none"} {diagnostics.LastEmergencyStopResultMessage ?? "none"}; watchdog stop-all {diagnostics.WatchdogStopAllCount:N0} {FormatTimestamp(diagnostics.LastWatchdogStopAllAtUtc)} {diagnostics.LastWatchdogStopAllMessage ?? "none"}; latency paddle-to-write {FormatMilliseconds(runtime.Latency.PaddleReceivedToStartWriteCompletedMs)}; flight recorder {runtime.FlightRecorderPath}; last suppression {snapshot.LastSuppressionReason ?? "none"}; last output {snapshot.LastOutputStatus ?? "none"}; runtime-only enable.";
    }

    private string BuildManualAsioHardwareTestDiagnosticsText()
    {
        var snapshot = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        return $"mode {snapshot.OutputMode}; ASIO status {BuildTrueAsioDiagnosticsText(snapshot)}; active {snapshot.IsActive}; haptics {snapshot.HapticsRunning}; emergency {snapshot.EmergencyMute}; normal mute {snapshot.NormalMute}; last source {snapshot.LastSource ?? "none"}; last signal {snapshot.LastTestSignal ?? "none"}; frequency {(snapshot.LastFrequencyHz is null ? "none" : $"{snapshot.LastFrequencyHz:0.#} Hz")}; duration {(snapshot.LastDurationMs is null ? "none" : $"{snapshot.LastDurationMs.Value:0} ms")}; duration mode {snapshot.LastDurationMode ?? "none"}; BST-1 duration mode {(_bst1PaddleGearSyncDuration ? "Sync" : "Custom")}; effective BST-1 duration {GetEffectiveBst1PaddleGearDurationMs()} ms; P-HPR gear duration {_sharedPhprGearPulseDurationMs} ms; custom BST-1 duration {_bst1PaddleGearCustomDurationMs} ms; flight recorder {snapshot.FlightRecorderPath}.";
    }

    private string BuildMockGearPulseDiagnosticsText()
    {
        var snapshot = _mockGearPulseRouter.GetSnapshot();
        var lastResult = snapshot.LastResult is null ? "none" : $"{snapshot.LastResult.Status}";
        var violation = snapshot.LastSafetyViolation?.Code.ToString() ?? "none";
        return $"{(snapshot.Options.IsEnabled ? "enabled" : "disabled")}; target {snapshot.Options.TargetModule}; strength {PhprUiValueConverter.FormatPercent(snapshot.Options.Profile.Strength01)}%; frequency {PhprUiValueConverter.FormatFrequency(snapshot.Options.Profile.FrequencyHz)} Hz; duration {snapshot.Options.Profile.DurationMs} ms; routed {snapshot.AcceptedRouteCount:N0}; ignored {snapshot.IgnoredRouteCount:N0}; safety rejected {snapshot.SafetyRejectedCount:N0}; last result {lastResult}; safety violation {violation}; mock commands {snapshot.OutputSnapshot.AcceptedCommandCount:N0}; mock frames {snapshot.OutputSnapshot.GeneratedFrameCount:N0}; pending stops {snapshot.OutputSnapshot.PendingScheduledStopCount:N0}; emergency stop {snapshot.EmergencyStopActive}; mock only, no hardware output.";
    }

    private string BuildMockPedalEffectsDiagnosticsText()
    {
        var snapshot = _mockPedalEffectsRouter.GetSnapshot();
        var lastResult = snapshot.LastResult is null ? "none" : $"{snapshot.LastResult.Status}";
        var violation = snapshot.LastSafetyViolation?.Code.ToString() ?? "none";
        return $"{(snapshot.Options.IsEnabled ? "enabled" : "disabled")}; {FormatPedalEffectDiagnostics(snapshot.RoadVibration)} {FormatPedalEffectDiagnostics(snapshot.WheelSlip)} {FormatPedalEffectDiagnostics(snapshot.WheelLock)} last result {lastResult}; safety violation {violation}; mock commands {snapshot.OutputSnapshot.AcceptedCommandCount:N0}; mock frames {snapshot.OutputSnapshot.GeneratedFrameCount:N0}; pending stops {snapshot.OutputSnapshot.PendingScheduledStopCount:N0}; emergency stop {snapshot.EmergencyStopActive}; mock only, no hardware output.";
    }

    private static string FormatDiscoveryMethods(IReadOnlyList<InputDiscoveryMethod> methods)
    {
        return methods.Count == 0 ? "none" : string.Join(", ", methods);
    }

    private static string FormatInputCandidates(IEnumerable<InputDeviceInfo> candidates)
    {
        var devices = candidates.Take(6).Select(FormatInputCandidate).ToArray();
        return devices.Length == 0 ? "none" : string.Join(" | ", devices);
    }

    private static string FormatInputCandidate(InputDeviceInfo device)
    {
        var vendorProduct = device.VendorId is null || device.ProductId is null
            ? "VID/PID unavailable"
            : $"VID_{device.VendorId:X4}/PID_{device.ProductId:X4}";
        var controls = device.ButtonCount is null && device.AxisCount is null
            ? "controls unavailable"
            : $"{device.ButtonCount?.ToString() ?? "unknown"} button(s), {device.AxisCount?.ToString() ?? "unknown"} axis/axes";
        return $"{device.DisplayName} via {device.DiscoveryMethod}; {vendorProduct}; {controls}; score {device.CandidateScore}; {device.CandidateReason}";
    }

    private static string FormatButtonMapping(int? buttonId)
    {
        return buttonId is null ? "unmapped" : $"button {buttonId}";
    }

    private static string FormatEnabled(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }

    private static string FormatOnOff(bool value)
    {
        return value ? "on" : "off";
    }

    private static string FormatActiveIdle(bool active)
    {
        return active ? "active" : "idle";
    }

    private static string FormatEnabledActive(bool enabled, bool active)
    {
        return enabled
            ? active ? "enabled/active" : "enabled/idle"
            : "disabled";
    }

    private static IReadOnlyList<PHprModuleId> ExpandBenchTarget(PHprGearPulseTarget target)
    {
        return target switch
        {
            PHprGearPulseTarget.Brake => [PHprModuleId.Brake],
            PHprGearPulseTarget.Throttle => [PHprModuleId.Throttle],
            PHprGearPulseTarget.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => [PHprModuleId.Brake]
        };
    }

    private static string FormatPhprCommand(PHprCommand? command)
    {
        return command is null
            ? "none"
            : $"{command.Source} -> {command.TargetModule}, strength {PhprUiValueConverter.FormatPercent(command.Strength01)}%, {PhprUiValueConverter.FormatFrequency(command.FrequencyHz)} Hz, {command.DurationMs} ms, priority {command.Priority}, flags {command.SafetyFlags}";
    }

    private static string FormatPedalEffectKind(PHprPedalEffectKind kind)
    {
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => "Road vibration",
            PHprPedalEffectKind.WheelSlip => "Wheel slip",
            PHprPedalEffectKind.WheelLock => "Wheel lock",
            _ => kind.ToString()
        };
    }

    private static string FormatPedalEffectState(PHprPedalEffectState state)
    {
        return $"{(state.IsEnabled ? "on" : "off")} {state.TargetModule} {PhprUiValueConverter.FormatPercent(state.Profile.Strength01)}%/{PhprUiValueConverter.FormatFrequency(state.Profile.FrequencyHz)} Hz/{state.Profile.DurationMs} ms";
    }

    private static string FormatRealPhprPulse(PHprRealGearPulseSettings settings)
    {
        return $"{(settings.IsEnabled ? "on" : "off")} {PhprUiValueConverter.FormatPercent(settings.Strength01)}%/{PhprUiValueConverter.FormatFrequency(settings.FrequencyHz)} Hz/{settings.DurationMs} ms";
    }

    private static string FormatRealRoadVibrationPedal(PHprRoadVibrationPedalSettings settings)
    {
        var normalized = settings.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return $"{(normalized.IsEnabled ? "on" : "off")} strength {PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01)}-{PhprUiValueConverter.FormatPercent(normalized.Strength01)}%; freq {PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz)}-{PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz)} Hz; duration {normalized.DurationMs} ms";
    }

    private static string FormatRealSlipLockEffect(
        PHprPedalEffectKind kind,
        PHprSlipLockEffectSettings settings)
    {
        var normalized = settings.Normalize(kind, SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return $"{(normalized.IsEnabled ? "on" : "off")} target {normalized.TargetModule}; strength {PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01)}-{PhprUiValueConverter.FormatPercent(normalized.Strength01)}%; freq {PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz)}-{PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz)} Hz; cadence {normalized.TextureCadenceMs} ms; duration {normalized.DurationMs} ms";
    }

    private static string FormatRealSlipLockEffectDiagnostics(
        PHprSlipLockEffectRoutingDiagnostics diagnostics,
        bool includeTelemetry)
    {
        var target = diagnostics.LastTargetModule?.ToString() ?? diagnostics.Settings.TargetModule.ToString();
        var telemetry = includeTelemetry
            ? $"; raw {FormatRealSlipLockTelemetry(diagnostics.Kind, diagnostics.LastTelemetry)}"
            : string.Empty;
        return $"{diagnostics.Kind}: active {diagnostics.LastActive}; reason {diagnostics.LastReason}; target {target}; intensity {diagnostics.LastIntensity01:0.###}; strength {PhprUiValueConverter.FormatPercent(diagnostics.LastComputedStrength01)}%; freq {PhprUiValueConverter.FormatFrequency(diagnostics.LastComputedFrequencyHz)} Hz; cadence {diagnostics.Settings.TextureCadenceMs:N0} ms; duration {diagnostics.LastCommandDurationMs:N0} ms; below tactile {diagnostics.LastBelowTactileThreshold}; routed {diagnostics.RouteCount:N0}; safety rejected {diagnostics.SafetyRejectedCount:N0}; interval suppressed {diagnostics.IntervalSuppressedCount:N0}; stale {diagnostics.StaleTelemetrySuppressedCount:N0}; command-rate {diagnostics.CommandRateSuppressedCount:N0}; stops {diagnostics.StopCommandCount:N0}; start age {FormatTimestampAge(diagnostics.LastStartAtUtc)}; update age {FormatTimestampAge(diagnostics.LastUpdateAtUtc)}; stop age {FormatTimestampAge(diagnostics.LastStopAtUtc)}{telemetry}";
    }

    private static string FormatRealSlipLockTelemetry(
        PHprPedalEffectKind kind,
        PHprSlipLockTelemetrySnapshot? telemetry)
    {
        if (telemetry is null)
        {
            return "none";
        }

        return kind == PHprPedalEffectKind.WheelLock
            ? $"speed {telemetry.SpeedKph:0.#} kph brake {telemetry.Brake01:0.##} slip-ratio {telemetry.MaximumSlipRatio:0.###} wheel-speed {telemetry.MinimumWheelSpeedMetersPerSecond:0.###} m/s ratio {telemetry.MinimumWheelSpeedRatio:0.###} ABS {telemetry.AntiLockBrakesActive} fresh T/M {telemetry.TelemetryFresh}/{telemetry.MotionExFresh}"
            : $"speed {telemetry.SpeedKph:0.#} kph throttle {telemetry.Throttle01:0.##} brake {telemetry.Brake01:0.##} slip-ratio {telemetry.MaximumSlipRatio:0.###} slip-angle {telemetry.MaximumSlipAngle:0.###} TC {telemetry.TractionControlActive} fresh T/M {telemetry.TelemetryFresh}/{telemetry.MotionExFresh}";
    }

    private static TimeSpan? DurationBetween(DateTimeOffset? start, DateTimeOffset? end)
    {
        return start is null || end is null ? null : end.Value - start.Value;
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "none" : timestamp.Value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestampAge(DateTimeOffset? timestamp)
    {
        return timestamp is null
            ? "none"
            : $"{Math.Max(0d, (DateTimeOffset.UtcNow - timestamp.Value).TotalMilliseconds):0} ms";
    }

    private static string FormatPedalEffectDiagnostics(PHprPedalEffectDiagnostics diagnostics)
    {
        return $"{FormatPedalEffectKind(diagnostics.Kind)}: {(diagnostics.State.IsEnabled ? "enabled" : "disabled")}; target {diagnostics.State.TargetModule}; active {diagnostics.IsActive}; intensity {diagnostics.Intensity01:0.###}; routed {diagnostics.RouteCount:N0}; safety rejected {diagnostics.SafetyRejectedCount:N0}; interval suppressed {diagnostics.IntervalSuppressedCount:N0}; last target {diagnostics.LastTargetModule?.ToString() ?? "none"}";
    }

    private static string FormatBoolUnknown(bool value, bool unknown)
    {
        return unknown ? "unknown" : value ? "yes" : "no";
    }

    private static string FormatScanTime(DateTimeOffset scanTimeUtc)
    {
        return scanTimeUtc == DateTimeOffset.MinValue
            ? "never"
            : scanTimeUtc.ToLocalTime().ToString("g");
    }

    private static string FormatDetectedSoftwareProcesses(IReadOnlyList<PHprDetectedSoftwareProcess> processes)
    {
        if (processes.Count == 0)
        {
            return "none";
        }

        return string.Join("; ", processes
            .Take(6)
            .Select(process => $"{process.ProcessName}{(process.ProcessId is null ? "" : $"#{process.ProcessId}")}"));
    }

    private static string FormatReadinessIssues(IReadOnlyList<PHprControlledWriteReadinessIssue> issues)
    {
        return issues.Count == 0
            ? "none"
            : string.Join("; ", issues.Take(8).Select(issue => $"{issue.Code}: {issue.Message}"));
    }

    private static string FormatPhprValidationIssues(IReadOnlyList<PHprManualValidationIssue> issues)
    {
        return issues.Count == 0
            ? "none"
            : string.Join("; ", issues.Take(8).Select(issue => $"{issue.Code}: {issue.Message}"));
    }

    private static string FormatOptionalInt(int? value)
    {
        return value is null ? "unknown" : value.Value.ToString("N0");
    }

    private static string FormatOptionalUInt(uint? value)
    {
        return value is null ? "unknown" : value.Value.ToString("N0");
    }

    private string BuildForwardingDestinationsText()
    {
        return _forwardingDestinations.Count == 0
            ? "none configured"
            : string.Join("; ", _forwardingDestinations.Select(destination =>
                $"{(destination.Enabled ? "enabled" : "disabled")} {destination.Name}->{destination.Host}:{destination.Port}"));
    }

    private string BuildTelemetryBindAddressText()
    {
        return _allowLanTelemetry ? "0.0.0.0" : IPAddress.Loopback.ToString();
    }

    private async Task RefreshAsioVisibilityDiagnosticsAsync()
    {
        _asioVisibilitySnapshot = await _asioVisibilityDiagnostics.RefreshAsync();
        UpdateAsioDriverSelectionItems();
        await RefreshAsioReadinessDiagnosticsAsync();
        UpdateDeviceStatus();
        UpdateDiagnosticsStatus();
    }

    private async Task ApplyStartupAsioDefaultsAsync()
    {
        if (_startupAsioDefaultsApplied)
        {
            return;
        }

        _startupAsioDefaultsApplied = true;
        var plan = StartupReadinessPlanner.BuildAsioSelectionPlan(
            _hasPersistedOutputModePreference,
            _selectedOutputKind,
            _selectedAsioDriverName,
            _selectedAsioOutputChannel,
            _asioArmed,
            _asioVisibilitySnapshot.DriverNames);
        _selectedOutputKind = plan.SelectedOutputKind;
        _selectedAsioDriverName = plan.SelectedAsioDriverName;
        _selectedAsioOutputChannel = plan.SelectedAsioOutputChannel;
        _asioArmed = plan.ArmAsioPreference;

        _updatingOutputUi = true;
        OutputModeComboBox.SelectedItem = _outputModeOptions.Single(option => option.Kind == _selectedOutputKind);
        UpdateAsioDriverSelectionItems();
        _updatingOutputUi = false;

        await RunSerializedLifecycleOperationAsync(
            (generation, cancellationToken) => RebuildHapticPipelineForOutputSelectionAsync(
                generation,
                plan.Message,
                cancellationToken),
            "Startup ASIO defaults application failed");
    }

    private async Task RefreshAsioReadinessDiagnosticsAsync()
    {
        _asioReadinessSnapshot = await _asioReadinessDiagnostics.RefreshAsync(_hapticPipeline.GetSnapshot().Output);
    }

    private void UpdateAsioDriverSelectionItems()
    {
        var drivers = _asioVisibilitySnapshot.DriverNames;
        _updatingOutputUi = true;
        AsioDriverComboBox.ItemsSource = drivers;

        if (_selectedAsioDriverName is not null
            && drivers.Contains(_selectedAsioDriverName, StringComparer.OrdinalIgnoreCase))
        {
            AsioDriverComboBox.SelectedItem = drivers.First(driver =>
                string.Equals(driver, _selectedAsioDriverName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            _selectedAsioDriverName = null;
            AsioDriverComboBox.SelectedIndex = -1;
        }

        AsioOutputChannelComboBox.SelectedItem = _selectedAsioOutputChannel;
        AsioArmCheckBox.IsChecked = _asioArmed;
        _updatingOutputUi = false;
    }
}

