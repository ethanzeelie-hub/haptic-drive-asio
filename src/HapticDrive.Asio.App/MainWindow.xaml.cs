using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime.Pipeline;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HapticDrive.Asio.App;

public partial class MainWindow : Window
{
    private readonly IAsioDriverCatalog _asioDriverCatalog = new WindowsRegistryAsioDriverCatalog();
    private readonly AsioDriverVisibilityDiagnostics _asioVisibilityDiagnostics;
    private readonly AsioReadinessDiagnostics _asioReadinessDiagnostics;
    private readonly AudioTestBench _testBench = new();
    private readonly IUdpTelemetryReceiver _telemetryReceiver = new UdpTelemetryReceiver();
    private readonly HapticProfileStore _profileStore = new();
    private readonly DispatcherTimer _telemetryStatusTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };
    private readonly IReadOnlyList<AudioTestSignalDefinition> _testBenchSignals =
    [
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Silence),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.SineTone),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.FrequencySweep),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Pulse),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Constant)
    ];
    private readonly IReadOnlyList<OutputModeOption> _outputModeOptions =
    [
        new(AudioOutputDeviceKind.Null, "Null output"),
        new(AudioOutputDeviceKind.Asio, "ASIO output"),
        new(AudioOutputDeviceKind.WasapiDebug, "WASAPI debug")
    ];
    private readonly IReadOnlyList<int> _asioOutputChannelChoices = Enumerable.Range(0, 8).ToArray();

    private readonly IReadOnlyList<ShellPageDefinition> _pages =
    [
        new(
            "Dashboard",
            "Dashboard",
            "A safe overview for raw UDP telemetry, output state, and hardware-absent operation.",
            "Stage 17 output-owned streaming pipeline",
            [
                "UDP listener starts on port 20778 by default.",
                "Packets are counted, preserved as raw datagrams, and offered to the forwarder.",
                "Recording captures raw incoming UDP payload bytes to a versioned replay file.",
                "Forwarding is byte-preserving and parser-independent.",
                "The F1 25 parser validates packet format, year, ID, version, exact length, and Stage 07 packet bodies.",
                "Stage 08 maps parsed packets into shared last-known VehicleState samples.",
                "Stage 10 adds a deterministic mixer, conservative safety chain, and null-output sample consumption.",
                "Stage 11 adds deterministic synthetic test signals for hardware-absent audio path validation.",
                "Stage 12 adds conservative engine vibration and gear shift effect generators from VehicleState.",
                "Stage 13 adds conservative kerb, impact, road texture, and slip / brake-lock effect generators from VehicleState.",
                "Stage 14 exposes safe tuning controls, profile save/load/reset, and practical runtime diagnostics.",
                "Stage 17 wires live UDP and replayed packets into the parser, VehicleState adapter, effects, mixer, safety chain, and output-owned renderer.",
                "Stale telemetry is muted by wall-clock timeout.",
                "NullAudioOutputDevice is the default safe output.",
                "The app remains safe to open without ASIO hardware or shaker hardware."
            ]),
        new(
            "Effects",
            "Effects",
            "Hardware-safe generated effect diagnostics.",
            "Stage 17 effect tuning drives the output-owned render path",
            [
                "Gear shift is synthesized from valid forward gear changes in VehicleState telemetry.",
                "Engine vibration is synthesized from RPM, throttle, idle RPM, max RPM, gear, speed, and pause/status gates where available.",
                "Kerb vibration is synthesized from rumble strip / ridged surface IDs, speed, and optional suspension/contact data.",
                "Impact pulses are synthesized from player collision events and abrupt vertical-G, wheel-force, or suspension-acceleration spikes.",
                "Road texture is synthesized from surface IDs, speed, and optional suspension / vertical-G motion.",
                "Slip and minimal brake-lock vibration are synthesized from wheel slip ratio/angle, wheel speed, throttle, brake, speed, TC, and ABS fields.",
                "Defaults are conservative and inspired by SimHub-style frequency, gain, pulse-duration, speed, and threshold controls.",
                "Live haptics render through an output-owned callback path; the test bench remains deterministic validation only.",
                "Advanced live graphs, routing matrices, and physical calibration remain deferred."
            ]),
        new(
            "Mixer / Routing",
            "Mixer / Routing",
            "Hardware-safe mixer level controls and safety-chain visibility.",
            "Mixer and safety controls available",
            [
                "The Stage 10 mixer can combine source buffers with source gain, master gain, mute, and emergency mute.",
                "The safety chain sanitises invalid samples, applies conservative output gain, limits peaks, and hard-clips overflow.",
                "Stage 12 and Stage 13 effect buffers feed this same mixer and safety path.",
                "Null output can consume final sample buffers deterministically without hardware.",
                "Mono BST-1 routing is the first hardware target.",
                "Future output adapters can be added without coupling effects to a specific device."
            ]),
        new(
            "Devices",
            "Devices",
            "Safe status for Null, WASAPI debug, and ASIO output abstractions.",
            "Output status available",
            [
                "NullAudioOutputDevice is available for automated tests and safe app startup.",
                "WasapiDebugOutputDevice exists as a manual debug placeholder only.",
                "AsioAudioOutputDevice exists behind the same interface and fails gracefully when no driver is available.",
                "WASAPI remains a manual debug fallback only.",
                "ASIO absence must fail gracefully and never block automated tests."
            ]),
        new(
            "Telemetry / UDP Router",
            "Telemetry / UDP Router",
            "Raw F1 25 UDP input, byte-preserving forwarding, and packet parser status.",
            "UDP listener, forwarder, recorder, parser, and VehicleState adapter active",
            [
                "Default listen port is 20778.",
                "Raw packets are counted and timestamped.",
                "Active recordings receive raw packet bytes before parser validation.",
                "Forwarding sends exact packet bytes to enabled destinations.",
                "Stage 07 packet bodies are parsed from the official F1 25 v3 spec.",
                "Stage 08 selects the player car from packet headers and keeps last-known VehicleState slices.",
                "Stage 12 and Stage 13 effects consume VehicleState only and do not read parser packet bodies directly.",
                "No forwarding destinations are configured in the shell yet.",
                "Replay is implemented in the Recording project for deterministic tests."
            ]),
        new(
            "Recordings",
            "Recordings",
            "Placeholder for telemetry capture and deterministic replay without running F1 25.",
            "Recording and replay service available",
            [
                "Use Start Recording to capture raw incoming UDP packets to a versioned file.",
                "Replay service emits recorded packets through the same parser and VehicleState path used by live packets.",
                "Deterministic VehicleState sequences can drive Stage 12 and Stage 13 effects in tests.",
                "Replay tests do not require F1 25, UDP sockets, audio output, ASIO hardware, or shaker hardware.",
                "A polished recording library UI and file picker are deferred."
            ]),
        new(
            "Test Bench",
            "Test Bench",
            "Safe synthetic signals for validating the internal audio path.",
            "Test bench available",
            [
                "Silence, sine tone, frequency sweep, pulse transient, and constant-value signals are available.",
                "Signals render through the existing mixer and safety chain into NullAudioOutputDevice.",
                "Start Test Bench renders deterministic validation buffers only; it is not a real audio callback.",
                "Emergency mute is applied through the test bench mixer and safety settings.",
                "Null output remains the automated-test target.",
                "Physical shaker tuning waits for later manual hardware stages."
            ]),
        new(
            "Profiles",
            "Profiles",
            "Versioned JSON tuning profiles for existing effects, mixer, and safety settings.",
            "Profiles available",
            [
                "The default profile uses conservative hardware-safe values.",
                "Profiles save and load as versioned JSON under local app data.",
                "Profile values are validated and repaired to safe software ranges on load.",
                "Emergency mute remains runtime-only and is not saved in profiles.",
                "Device settings remain separate from effect profiles."
            ]),
        new(
            "Settings",
            "Settings",
            "Safe app preferences, theme selection, and conservative reset.",
            "Settings available",
            [
                "Dark theme is active by default; the light theme button currently demonstrates theme scaffolding.",
                "Close/minimize-to-tray support is represented by the disabled footer setting.",
                "No setting should require admin rights or physical haptic hardware."
            ]),
        new(
            "Diagnostics",
            "Diagnostics",
            "Packet rate, parser errors, output status, peak levels, limiter activity, and status snapshots.",
            "Diagnostics available",
            [
                "Output status is available for the selected safe output device.",
                "Mixer and safety diagnostics are available in the audio pipeline tests and minimal shell status.",
                "Stage 17 reports pipeline, engine, gear, kerb, impact, road texture, slip, mixer, safety, output, replay, ASIO visibility, callback, drop, underrun, jitter, and telemetry-age state.",
                "Test bench diagnostics report selected synthetic signal, output peak, limiter count, and output mode.",
                "UDP packet count, packet rate, and no-packet warning are available.",
                "Forwarded datagram count, forwarded byte count, and forwarding errors are available.",
                "F1 25 packet parser success, ignored, and failure counts are available.",
                "VehicleState update count, player index, speed, and gear are available when telemetry packets arrive.",
                "Diagnostics become more meaningful as telemetry, parser, audio, and replay stages are implemented.",
                "Logging must not block telemetry, UI, disk, or audio paths.",
                "Diagnostics can be refreshed manually and remain safe without telemetry or hardware."
            ])
    ];

    private bool _hapticsStarted;
    private bool _emergencyMuted;
    private bool _lightTheme;
    private bool _updatingOutputUi;
    private AudioOutputDeviceKind _selectedOutputKind = AudioOutputDeviceKind.Null;
    private string? _selectedAsioDriverName;
    private int? _selectedAsioOutputChannel;
    private bool _asioArmed;
    private string? _telemetryStartError;
    private string? _recordingError;
    private string? _replayError;
    private HapticDriveProfile _currentProfile = HapticDriveProfile.Default;
    private HapticPipelineCoordinator _hapticPipeline = new();
    private AsioDriverVisibilitySnapshot _asioVisibilitySnapshot = AsioDriverVisibilitySnapshot.NotChecked;
    private AsioReadinessSnapshot _asioReadinessSnapshot = AsioReadinessSnapshot.NotChecked;
    private Task? _activeReplayTask;
    private bool _updatingTuningUi;
    private bool _updatingSettingsUi;

    public MainWindow()
    {
        _asioVisibilityDiagnostics = new AsioDriverVisibilityDiagnostics(_asioDriverCatalog);
        _asioReadinessDiagnostics = new AsioReadinessDiagnostics(_asioDriverCatalog);

        InitializeComponent();

        NavigationList.ItemsSource = _pages;
        NavigationList.SelectedIndex = 0;
        _updatingOutputUi = true;
        OutputModeComboBox.ItemsSource = _outputModeOptions;
        OutputModeComboBox.SelectedItem = _outputModeOptions.Single(option => option.Kind == _selectedOutputKind);
        AsioOutputChannelComboBox.ItemsSource = _asioOutputChannelChoices;
        _updatingOutputUi = false;
        TestBenchSignalComboBox.ItemsSource = _testBenchSignals;
        TestBenchSignalComboBox.SelectedIndex = 1;
        ApplyTheme(lightTheme: false);
        ApplyProfileToControls(_currentProfile);
        ApplyProfileToRuntime(_currentProfile);
        UpdateProfileStatus("Default conservative profile loaded.", []);
        _telemetryReceiver.PacketReceived += TelemetryReceiver_PacketReceived;
        _telemetryStatusTimer.Tick += TelemetryStatusTimer_Tick;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsioVisibilityDiagnosticsAsync();

        try
        {
            await _telemetryReceiver.StartAsync();
            _telemetryStatusTimer.Start();
        }
        catch (Exception ex)
        {
            _telemetryStartError = ex.Message;
        }

        UpdateTelemetryStatus();
        UpdateMixerStatus();
        UpdateDeviceStatus();
        UpdateOutputStatus(_hapticPipeline.GetSnapshot().Output);
        UpdateProfileStatus();
        UpdateTestBenchStatus();
        UpdateDiagnosticsStatus();
    }

    private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationList.SelectedItem is ShellPageDefinition page)
        {
            PageTitleText.Text = page.Title;
            PageSummaryText.Text = page.Summary;
            PageStatusText.Text = page.Status;
            PageItemsControl.ItemsSource = page.Items;
            EffectsPanel.Visibility = page.NavigationLabel == "Effects"
                ? Visibility.Visible
                : Visibility.Collapsed;
            MixerPanel.Visibility = page.NavigationLabel == "Mixer / Routing"
                ? Visibility.Visible
                : Visibility.Collapsed;
            DevicesPanel.Visibility = page.NavigationLabel == "Devices"
                ? Visibility.Visible
                : Visibility.Collapsed;
            RecordingsPanel.Visibility = page.NavigationLabel == "Recordings"
                ? Visibility.Visible
                : Visibility.Collapsed;
            TestBenchPanel.Visibility = page.NavigationLabel == "Test Bench"
                ? Visibility.Visible
                : Visibility.Collapsed;
            ProfilesPanel.Visibility = page.NavigationLabel == "Profiles"
                ? Visibility.Visible
                : Visibility.Collapsed;
            SettingsPanel.Visibility = page.NavigationLabel == "Settings"
                ? Visibility.Visible
                : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = page.NavigationLabel == "Diagnostics"
                ? Visibility.Visible
                : Visibility.Collapsed;
            FooterStatusText.Text = $"Viewing {page.NavigationLabel} - Stage 17 streaming pipeline";
            UpdateTelemetryStatus();
            UpdateEffectStatus();
            UpdateMixerStatus();
            UpdateDeviceStatus();
            UpdateProfileStatus();
            UpdateTestBenchStatus();
            UpdateDiagnosticsStatus();
        }
    }

    private async void OutputModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputUi
            || OutputModeComboBox.SelectedItem is not OutputModeOption option)
        {
            return;
        }

        _selectedOutputKind = option.Kind;
        await RebuildHapticPipelineForOutputSelectionAsync($"Output mode changed to {option.Label}; haptics are stopped until started explicitly.");
    }

    private async void AsioDriverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingOutputUi)
        {
            return;
        }

        _selectedAsioDriverName = AsioDriverComboBox.SelectedItem as string;
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            await RebuildHapticPipelineForOutputSelectionAsync("ASIO driver selection changed; haptics are stopped until ASIO is armed and started explicitly.");
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
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            await RebuildHapticPipelineForOutputSelectionAsync("ASIO channel selection changed; haptics are stopped until started explicitly.");
        }
    }

    private async void AsioArmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingOutputUi)
        {
            return;
        }

        _asioArmed = AsioArmCheckBox.IsChecked == true;
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            await RebuildHapticPipelineForOutputSelectionAsync(
                _asioArmed
                    ? "ASIO armed. Start Haptics is still required before output can run."
                    : "ASIO disarmed and haptics stopped.");
        }
    }

    private async void RefreshAsioButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsioVisibilityDiagnosticsAsync();
        FooterStatusText.Text = "ASIO readiness diagnostics refreshed.";
    }

    private async Task RebuildHapticPipelineForOutputSelectionAsync(string footerMessage)
    {
        if (_hapticsStarted || _hapticPipeline.GetSnapshot().IsRunning)
        {
            await _hapticPipeline.StopAsync();
        }

        _hapticsStarted = false;
        StartStopButton.Content = "Start Haptics";

        var previousPipeline = _hapticPipeline;
        _hapticPipeline = CreatePipelineForSelectedOutput();
        _hapticPipeline.ApplyProfile(_currentProfile);
        await previousPipeline.DisposeAsync();

        await RefreshAsioReadinessDiagnosticsAsync();
        UpdateHapticsStateText();
        UpdateRecordingStatus();
        UpdateOutputStatus(_hapticPipeline.GetSnapshot().Output);
        UpdateDeviceStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = footerMessage;
    }

    private HapticPipelineCoordinator CreatePipelineForSelectedOutput()
    {
        var configuration = BuildSelectedOutputConfiguration();
        return new HapticPipelineCoordinator(
            configuration,
            CreateSelectedOutputDevice(),
            profile: _currentProfile);
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

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _hapticsStarted
            ? await _hapticPipeline.StopAsync()
            : await _hapticPipeline.StartAsync();

        if (!result.Succeeded)
        {
            FooterStatusText.Text = result.Message;
            if (result.OutputResult is not null)
            {
                UpdateOutputStatus(result.OutputResult.Status);
            }

            return;
        }

        _hapticsStarted = !_hapticsStarted;
        StartStopButton.Content = _hapticsStarted ? "Stop Haptics" : "Start Haptics";
        UpdateHapticsStateText();
        FooterStatusText.Text = _hapticsStarted
            ? "Haptics started with output-owned low-latency rendering; Null output remains the default unless ASIO was selected, routed, and armed."
            : "Haptics stopped";
        UpdateOutputStatus(result.OutputResult?.Status ?? _hapticPipeline.GetSnapshot().Output);
        UpdateEffectStatus();
        UpdateDiagnosticsStatus();
    }

    private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _hapticPipeline.RecordingService.GetSnapshot();
        if (snapshot.IsRecording)
        {
            var stopResult = await _hapticPipeline.RecordingService.StopAsync();
            FooterStatusText.Text = stopResult.Message;
            if (!stopResult.Succeeded)
            {
                _recordingError = stopResult.Message;
            }

            UpdateRecordingStatus();
            return;
        }

        try
        {
            var path = CreateDefaultRecordingPath();
            var startResult = await _hapticPipeline.RecordingService.StartAsync(path);
            if (startResult.Succeeded)
            {
                _recordingError = null;
                FooterStatusText.Text = $"Recording raw UDP packets to {Path.GetFileName(path)}.";
            }
            else
            {
                _recordingError = startResult.Message;
                FooterStatusText.Text = startResult.Message;
            }
        }
        catch (Exception ex)
        {
            _recordingError = ex.Message;
            FooterStatusText.Text = $"Recording could not start: {ex.Message}";
        }

        UpdateRecordingStatus();
    }

    private async void StartReplayButton_Click(object sender, RoutedEventArgs e)
    {
        var replaySnapshot = _hapticPipeline.GetSnapshot().Replay;
        if (replaySnapshot.IsReplaying)
        {
            await _hapticPipeline.ReplayService.StopAsync();
            FooterStatusText.Text = "Replay stop requested.";
            UpdateRecordingStatus();
            UpdateDiagnosticsStatus();
            return;
        }

        var path = FindLatestRecordingPath();
        if (path is null)
        {
            _replayError = "No local .hdrec recording is available to replay yet.";
            FooterStatusText.Text = _replayError;
            UpdateRecordingStatus();
            return;
        }

        _replayError = null;
        FooterStatusText.Text = $"Replaying {Path.GetFileName(path)} through the output-owned haptic pipeline.";
        _activeReplayTask = ReplayLatestRecordingAsync(path);
        UpdateRecordingStatus();
    }

    private async Task ReplayLatestRecordingAsync(string path)
    {
        var result = await _hapticPipeline.ReplayFileAsync(path, TelemetryReplayOptions.Fast);
        await Dispatcher.InvokeAsync(() =>
        {
            _replayError = result.Succeeded ? null : result.Message;
            FooterStatusText.Text = result.Message;
            UpdateTelemetryStatus();
            UpdateRecordingStatus();
            UpdateEffectStatus();
            UpdateDiagnosticsStatus();
        });
    }

    private async void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        _emergencyMuted = !_emergencyMuted;
        var pipelineMuteResult = await _hapticPipeline.SetEmergencyMuteAsync(_emergencyMuted);
        _testBench.EmergencyMute = _emergencyMuted;
        EmergencyMuteButton.Content = _emergencyMuted ? "Clear Mute" : "Emergency Mute";
        UpdateHapticsStateText();

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

        FooterStatusText.Text = _emergencyMuted
            ? "Emergency mute is active in the mixer, safety chain, and test bench."
            : "Emergency mute cleared in the mixer, safety chain, and test bench.";
        UpdateEffectStatus();
        UpdateMixerStatus();
        UpdateTestBenchStatus();
    }

    private void UpdateHapticsStateText()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();

        if (_emergencyMuted)
        {
            HapticsStateText.Text = "Emergency muted";
            return;
        }

        if (!pipelineSnapshot.IsRunning)
        {
            HapticsStateText.Text = "Stopped";
            return;
        }

        if (pipelineSnapshot.TelemetryTimedOutMuted)
        {
            HapticsStateText.Text = "Telemetry stale mute";
            return;
        }

        HapticsStateText.Text = pipelineSnapshot.Audio is null
            ? "Mixer idle"
            : $"{pipelineSnapshot.Effects.ActiveEffectCount} effect(s); peak {pipelineSnapshot.Audio.OutputPeakLevel:0.000}";
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _lightTheme = !_lightTheme;
        ApplyTheme(_lightTheme);
    }

    private void ApplyTheme(bool lightTheme)
    {
        _lightTheme = lightTheme;
        var palette = lightTheme
            ? new ThemePalette("#F5F7FA", "#FFFFFF", "#E9EEF4", "#CBD5E1", "#17212B", "#5F6C7B", "#187CA8", "#C23B35", "#1D8A60")
            : new ThemePalette("#0B0F14", "#111820", "#17212B", "#263241", "#E8EEF6", "#99A8B8", "#3BAFDA", "#E5534B", "#39B980");

        Resources["AppBackgroundBrush"] = BrushFrom(palette.Background);
        Resources["AppSurfaceBrush"] = BrushFrom(palette.Surface);
        Resources["AppSurfaceAltBrush"] = BrushFrom(palette.SurfaceAlt);
        Resources["AppBorderBrush"] = BrushFrom(palette.Border);
        Resources["AppTextBrush"] = BrushFrom(palette.Text);
        Resources["AppMutedTextBrush"] = BrushFrom(palette.MutedText);
        Resources["AppAccentBrush"] = BrushFrom(palette.Accent);
        Resources["AppDangerBrush"] = BrushFrom(palette.Danger);
        Resources["AppSuccessBrush"] = BrushFrom(palette.Success);
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
        ApplyProfileToRuntime(profile);
        UpdateProfileControlText(profile);
        UpdateMixerStatus();
        UpdateEffectStatus();
        UpdateDiagnosticsStatus();

        if (_hapticsStarted)
        {
            FooterStatusText.Text = "Tuning applied to the output-owned render path.";
        }
        else
        {
            FooterStatusText.Text = "Tuning applied; haptics are still stopped.";
        }
    }

    private void ThemeSettingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingSettingsUi)
        {
            return;
        }

        ApplyTheme(SettingsLightThemeCheckBox.IsChecked == true);
        UpdateProfileStatus();
    }

    private HapticDriveProfile BuildProfileFromControls()
    {
        var name = string.IsNullOrWhiteSpace(ProfileNameTextBox.Text)
            ? _currentProfile.Name
            : ProfileNameTextBox.Text.Trim();
        var effects = _currentProfile.Effects;
        var engineMinimumFrequency = (float)EngineMinimumFrequencySlider.Value;
        var engineMaximumFrequency = Math.Max(engineMinimumFrequency, (float)EngineMaximumFrequencySlider.Value);

        return HapticProfileValidator.Validate(_currentProfile with
        {
            Name = name,
            Effects = effects with
            {
                Engine = effects.Engine with
                {
                    IsEnabled = EngineEnabledCheckBox.IsChecked == true,
                    Gain = (float)EngineGainSlider.Value,
                    MinimumFrequencyHz = engineMinimumFrequency,
                    MaximumFrequencyHz = engineMaximumFrequency
                },
                GearShift = effects.GearShift with
                {
                    IsEnabled = GearShiftEnabledCheckBox.IsChecked == true,
                    Gain = (float)GearShiftGainSlider.Value,
                    PulseDurationMilliseconds = (int)Math.Round(GearShiftDurationSlider.Value)
                },
                Kerb = effects.Kerb with
                {
                    IsEnabled = KerbEnabledCheckBox.IsChecked == true,
                    Gain = (float)KerbGainSlider.Value,
                    BaseFrequencyHz = (float)KerbBaseFrequencySlider.Value
                },
                Impact = effects.Impact with
                {
                    IsEnabled = ImpactEnabledCheckBox.IsChecked == true,
                    Gain = (float)ImpactGainSlider.Value,
                    PulseDurationMilliseconds = (int)Math.Round(ImpactDurationSlider.Value)
                },
                RoadTexture = effects.RoadTexture with
                {
                    IsEnabled = RoadTextureEnabledCheckBox.IsChecked == true,
                    Gain = (float)RoadTextureGainSlider.Value,
                    MinimumSpeedKph = (float)RoadTextureMinimumSpeedSlider.Value
                },
                Slip = effects.Slip with
                {
                    IsEnabled = SlipEnabledCheckBox.IsChecked == true,
                    Gain = (float)SlipGainSlider.Value,
                    SlipRatioThreshold = (float)SlipThresholdSlider.Value
                }
            },
            Mixer = _currentProfile.Mixer with
            {
                MasterGain = (float)MasterGainSlider.Value,
                IsMuted = MixerMuteCheckBox.IsChecked == true
            },
            Safety = _currentProfile.Safety with
            {
                OutputGain = (float)SafetyOutputGainSlider.Value,
                OutputGainCeiling = (float)SafetyOutputCeilingSlider.Value,
                LimiterEnabled = LimiterEnabledCheckBox.IsChecked == true
            }
        }).Profile;
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
    }

    private void ApplyProfileToControls(HapticDriveProfile profile)
    {
        var validation = HapticProfileValidator.Validate(profile);
        var safeProfile = validation.Profile;
        _updatingTuningUi = true;

        ProfileNameTextBox.Text = safeProfile.Name;
        EngineEnabledCheckBox.IsChecked = safeProfile.Effects.Engine.IsEnabled;
        EngineGainSlider.Value = safeProfile.Effects.Engine.Gain;
        EngineMinimumFrequencySlider.Value = safeProfile.Effects.Engine.MinimumFrequencyHz;
        EngineMaximumFrequencySlider.Value = safeProfile.Effects.Engine.MaximumFrequencyHz;
        GearShiftEnabledCheckBox.IsChecked = safeProfile.Effects.GearShift.IsEnabled;
        GearShiftGainSlider.Value = safeProfile.Effects.GearShift.Gain;
        GearShiftDurationSlider.Value = safeProfile.Effects.GearShift.PulseDurationMilliseconds;
        KerbEnabledCheckBox.IsChecked = safeProfile.Effects.Kerb.IsEnabled;
        KerbGainSlider.Value = safeProfile.Effects.Kerb.Gain;
        KerbBaseFrequencySlider.Value = safeProfile.Effects.Kerb.BaseFrequencyHz;
        ImpactEnabledCheckBox.IsChecked = safeProfile.Effects.Impact.IsEnabled;
        ImpactGainSlider.Value = safeProfile.Effects.Impact.Gain;
        ImpactDurationSlider.Value = safeProfile.Effects.Impact.PulseDurationMilliseconds;
        RoadTextureEnabledCheckBox.IsChecked = safeProfile.Effects.RoadTexture.IsEnabled;
        RoadTextureGainSlider.Value = safeProfile.Effects.RoadTexture.Gain;
        RoadTextureMinimumSpeedSlider.Value = safeProfile.Effects.RoadTexture.MinimumSpeedKph;
        SlipEnabledCheckBox.IsChecked = safeProfile.Effects.Slip.IsEnabled;
        SlipGainSlider.Value = safeProfile.Effects.Slip.Gain;
        SlipThresholdSlider.Value = safeProfile.Effects.Slip.SlipRatioThreshold;
        MasterGainSlider.Value = safeProfile.Mixer.MasterGain;
        MixerMuteCheckBox.IsChecked = safeProfile.Mixer.IsMuted;
        SafetyOutputGainSlider.Value = safeProfile.Safety.OutputGain;
        SafetyOutputCeilingSlider.Value = safeProfile.Safety.OutputGainCeiling;
        LimiterEnabledCheckBox.IsChecked = safeProfile.Safety.LimiterEnabled;

        _updatingTuningUi = false;
        UpdateProfileControlText(safeProfile);
    }

    private void UpdateProfileControlText(HapticDriveProfile profile)
    {
        EngineGainValueText.Text = $"{profile.Effects.Engine.Gain:P0}";
        EngineFrequencyValueText.Text = $"{profile.Effects.Engine.MinimumFrequencyHz:0}-{profile.Effects.Engine.MaximumFrequencyHz:0} Hz";
        GearShiftGainValueText.Text = $"{profile.Effects.GearShift.Gain:P0}";
        GearShiftDurationValueText.Text = $"{profile.Effects.GearShift.PulseDurationMilliseconds} ms";
        KerbGainValueText.Text = $"{profile.Effects.Kerb.Gain:P0}";
        KerbFrequencyValueText.Text = $"{profile.Effects.Kerb.BaseFrequencyHz:0} Hz";
        ImpactGainValueText.Text = $"{profile.Effects.Impact.Gain:P0}";
        ImpactDurationValueText.Text = $"{profile.Effects.Impact.PulseDurationMilliseconds} ms";
        RoadTextureGainValueText.Text = $"{profile.Effects.RoadTexture.Gain:P0}";
        RoadTextureMinimumSpeedValueText.Text = $"{profile.Effects.RoadTexture.MinimumSpeedKph:0} km/h";
        SlipGainValueText.Text = $"{profile.Effects.Slip.Gain:P0}";
        SlipThresholdValueText.Text = $"{profile.Effects.Slip.SlipRatioThreshold:0.00}";
        MasterGainValueText.Text = $"{profile.Mixer.MasterGain:P0}";
        SafetyOutputGainValueText.Text = $"{profile.Safety.OutputGain:P0}";
        SafetyOutputCeilingValueText.Text = $"{profile.Safety.OutputGainCeiling:0.00}";
    }

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = BuildProfileFromControls();
        var result = await _profileStore.SaveAsync(profile, HapticProfileStore.GetDefaultProfilePath());

        if (result.Succeeded)
        {
            _currentProfile = HapticProfileValidator.Validate(profile).Profile;
            ApplyProfileToControls(_currentProfile);
            ApplyProfileToRuntime(_currentProfile);
        }

        UpdateProfileStatus(result.Message, result.ValidationMessages);
        FooterStatusText.Text = result.Succeeded
            ? $"Saved profile {Path.GetFileName(result.Path)}."
            : result.Message;
    }

    private async void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _profileStore.LoadAsync(HapticProfileStore.GetDefaultProfilePath());
        if (result.Succeeded && result.Profile is not null)
        {
            ApplyProfileToControls(result.Profile);
            ApplyProfileToRuntime(result.Profile);
            UpdateEffectStatus();
            UpdateMixerStatus();
        }

        UpdateProfileStatus(result.Message, result.ValidationMessages);
        FooterStatusText.Text = result.Message;
    }

    private void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyProfileToControls(HapticDriveProfile.Default);
        ApplyProfileToRuntime(HapticDriveProfile.Default);
        UpdateEffectStatus();
        UpdateMixerStatus();
        UpdateProfileStatus("Reset to conservative defaults.", []);

        if (_hapticsStarted)
        {
            FooterStatusText.Text = "Reset tuning to conservative defaults for the output-owned render path.";
            return;
        }

        FooterStatusText.Text = "Reset tuning to conservative defaults.";
    }

    private void RefreshDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Diagnostics refreshed.";
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

    private void UpdateTestBenchStatus()
    {
        var snapshot = _testBench.GetSnapshot();

        TestBenchStartStopButton.Content = snapshot.IsActive ? "Stop Test Bench" : "Start Test Bench";
        TestBenchStateText.Text = snapshot.EmergencyMute
            ? "Emergency muted"
            : snapshot.IsActive
                ? $"Active: {snapshot.SelectedSignalName}"
                : "Idle";
        TestBenchPeakText.Text = snapshot.OutputPeakLevel.ToString("0.000");
        TestBenchLimiterText.Text = $"{snapshot.LimitedSampleCount:N0} limited";
        TestBenchOutputText.Text = $"{snapshot.OutputDisplayName} ({snapshot.OutputState})";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Test Bench" })
        {
            PageStatusText.Text = snapshot.IsActive
                ? $"{snapshot.SelectedSignalName}; {snapshot.RenderedBufferCount:N0} buffer(s); peak {snapshot.OutputPeakLevel:0.000}; limiter {snapshot.LimitedSampleCount:N0} sample(s); mute {(snapshot.IsMuted ? "on" : "off")}; emergency {snapshot.EmergencyMute}."
                : $"Test bench idle; output {snapshot.OutputDisplayName}; mute {(snapshot.IsMuted ? "on" : "off")}; emergency {snapshot.EmergencyMute}.";
        }

        UpdateDiagnosticsStatus();
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

    private void UpdateOutputStatus(AudioOutputStatus status)
    {
        OutputModeValueText.Text = status.DisplayName;
        OutputModeDetailText.Text = status.Kind == AudioOutputDeviceKind.Asio
            ? $"{status.StatusMessage} Armed {status.IsHardwareArmed}; channel {(status.SelectedOutputChannel is null ? "not selected" : status.SelectedOutputChannel)}."
            : status.StatusMessage;
        UpdateDeviceStatus();
    }

    private void UpdateMixerStatus()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var mixer = _currentProfile.ToMixerSettings(_emergencyMuted);
        var safety = _currentProfile.ToSafetyOptions(_emergencyMuted);
        MasterGainValueText.Text = $"{mixer.MasterGain:P0}";
        SafetyOutputGainValueText.Text = $"{safety.OutputGain:P0}";
        SafetyOutputCeilingValueText.Text = $"{safety.OutputGainCeiling:0.00}";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Mixer / Routing" })
        {
            var peak = pipelineSnapshot.Audio?.OutputPeakLevel ?? 0f;
            PageStatusText.Text = $"Master {mixer.MasterGain:P0}; mute {(pipelineSnapshot.IsMuted ? "on" : "off")}; emergency mute {(_emergencyMuted ? "on" : "off")}; output peak {peak:0.000}.";
        }
    }

    private void UpdateDeviceStatus()
    {
        var snapshot = _hapticPipeline.GetSnapshot();
        var status = snapshot.Output;
        CurrentOutputStatusText.Text = $"Current output: {status.DisplayName} ({status.State}); {status.StatusMessage}";
        NullOutputStatusText.Text = "Null output: default automated-test and hardware-absent target; produces no physical sound.";
        WasapiDebugStatusText.Text = "WASAPI debug: manual placeholder only; it is not the ASIO target and is never selected automatically.";
        AsioStatusText.Text = $"{_asioVisibilitySnapshot.Message} Windows sound output visibility is not proof of ASIO usage; Null output remains default.";
        AsioReadinessStatusText.Text = $"{_asioReadinessSnapshot.Message} Selected driver {(_selectedAsioDriverName ?? "none")}; selected channel {(_selectedAsioOutputChannel is null ? "none" : _selectedAsioOutputChannel)}; armed {_asioArmed}; render callbacks {status.RenderCallbackCount:N0}; backend callbacks {status.BackendCallbackCount:N0}; submitted {status.SubmittedBufferCount:N0}; dropped {status.DroppedBufferCount:N0}; underruns {status.UnderrunCount:N0}; last error {status.LastError ?? "none"}.";
        HardwareChainStatusText.Text = _asioReadinessSnapshot.HardwareChainWarning;

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Devices" })
        {
            PageStatusText.Text = status.RequiresPhysicalHardware
                ? "Selected output requires explicit manual hardware readiness checks; haptics remain stopped until armed and started."
                : $"Hardware-absent mode active; NullAudioOutputDevice remains the safe default; ASIO drivers reported {_asioVisibilitySnapshot.DriverNames.Count}.";
        }
    }

    private void UpdateProfileStatus(string? message = null, IReadOnlyList<string>? validationMessages = null)
    {
        var path = HapticProfileStore.GetDefaultProfilePath();
        ProfileStatusText.Text = message ?? $"Active profile: {_currentProfile.Name}.";
        ProfilePathText.Text = $"Default profile path: {path}";
        ProfileValidationText.Text = validationMessages is { Count: > 0 }
            ? string.Join(" ", validationMessages)
            : "Profile values are clamped to conservative software ranges on load and save.";
        SettingsStatusText.Text = $"Theme: {(_lightTheme ? "Light" : "Dark")}. Active profile: {_currentProfile.Name}. Default output remains NullAudioOutputDevice.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Profiles" })
        {
            PageStatusText.Text = $"Active profile {_currentProfile.Name}; JSON version {HapticDriveProfile.CurrentVersion}; emergency mute is not saved.";
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Settings" })
        {
            PageStatusText.Text = $"Theme {(_lightTheme ? "light" : "dark")}; conservative defaults available; profile storage is local app data.";
        }
    }

    private void UpdateDiagnosticsStatus()
    {
        if (DiagnosticsPanel.Visibility != Visibility.Visible
            && NavigationList.SelectedItem is not ShellPageDefinition { NavigationLabel: "Diagnostics" })
        {
            return;
        }

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
        var forwarderSnapshot = pipelineSnapshot.Forwarding;
        var recordingSnapshot = pipelineSnapshot.Recording;
        var replaySnapshot = pipelineSnapshot.Replay;
        var parserSuccess = pipelineSnapshot.ParserSuccessCount;
        var parserIgnored = pipelineSnapshot.ParserIgnoredCount;
        var parserFailed = pipelineSnapshot.ParserFailureCount;
        var vehicleUpdates = pipelineSnapshot.VehicleStateUpdateCount;

        DiagnosticsSummaryText.Text = $"UDP {receiverSnapshot.PacketCount:N0} packet(s), parser {parserSuccess:N0} valid / {parserFailed:N0} failed, effects {audioDiagnostics.ActiveEffectCount}, output peak {audioDiagnostics.OutputPeakLevel:0.000}, callbacks {outputStatus.RenderCallbackCount:N0}.";
        DiagnosticsItemsControl.ItemsSource = new[]
        {
            $"Pipeline: {(pipelineSnapshot.IsRunning ? "running" : "stopped")}; source {pipelineSnapshot.InputSource}; rendered {pipelineSnapshot.RenderedBufferCount:N0} buffer(s); telemetry age {(pipelineSnapshot.TelemetryAge is null ? "none" : $"{pipelineSnapshot.TelemetryAge.Value.TotalMilliseconds:0} ms")}; stale mute {pipelineSnapshot.TelemetryTimedOutMuted}; last error {pipelineSnapshot.LastPipelineError ?? "none"}.",
            $"UDP listener: {(receiverSnapshot.IsRunning ? "running" : "stopped")} on port {receiverSnapshot.BoundPort}; rate {receiverSnapshot.PacketRatePerSecond:0.00}/s; last packet {(receiverSnapshot.LastPacketAtUtc is null ? "never" : $"{receiverSnapshot.TimeSinceLastPacket?.TotalSeconds:0.0}s ago")}.",
            $"UDP forwarding: {forwarderSnapshot.EnabledDestinationCount}/{forwarderSnapshot.DestinationCount} destination(s) enabled; {forwarderSnapshot.ForwardedDatagramCount:N0} datagrams; {forwarderSnapshot.ErrorCount:N0} error(s).",
            $"Parser: {parserSuccess:N0} valid, {parserIgnored:N0} ignored, {parserFailed:N0} failed. {pipelineSnapshot.LastPacketMessage}",
            $"VehicleState: {vehicleUpdates:N0} update(s). {pipelineSnapshot.LastVehicleStateMessage}",
            $"Recording: {(recordingSnapshot.IsRecording ? "active" : "inactive")}; {recordingSnapshot.PacketCount:N0} packet(s); file {(recordingSnapshot.FilePath is null ? "none" : Path.GetFileName(recordingSnapshot.FilePath))}.",
            $"Replay: {(replaySnapshot.IsReplaying ? "active" : "inactive")}; {replaySnapshot.PacketsReplayed:N0} packet(s); {replaySnapshot.StatusMessage}",
            $"Effects: enabled engine {effectSnapshot.Engine.IsEnabled}, gear {effectSnapshot.GearShift.IsEnabled}, kerb {effectSnapshot.Kerb.IsEnabled}, impact {effectSnapshot.Impact.IsEnabled}, road {effectSnapshot.RoadTexture.IsEnabled}, slip {effectSnapshot.Slip.IsEnabled}; peak {effectSnapshot.PeakLevel:0.000}.",
            $"Mixer / safety: mixer peak {audioDiagnostics.MixerPeakLevel:0.000}; output peak {audioDiagnostics.OutputPeakLevel:0.000}; limited {audioDiagnostics.LimitedSampleCount:N0}; clipped {audioDiagnostics.ClippedSampleCount:N0}; emergency mute {audioDiagnostics.EmergencyMute}.",
            $"Test bench: {(testBenchSnapshot.IsActive ? "active" : "inactive")}; signal {testBenchSnapshot.SelectedSignalName}; output {testBenchSnapshot.OutputDisplayName}; peak {testBenchSnapshot.OutputPeakLevel:0.000}.",
            $"Output: {outputStatus.DisplayName} ({outputStatus.State}); streaming {outputStatus.IsStreaming}; hardware required {outputStatus.RequiresPhysicalHardware}; manual debug {outputStatus.IsManualDebugOnly}; hardware-absent mode {audioDiagnostics.HardwareAbsentMode}; null buffers {pipelineSnapshot.NullOutput?.SubmittedBufferCount ?? 0:N0}; render callbacks {outputStatus.RenderCallbackCount:N0}; backend callbacks {outputStatus.BackendCallbackCount:N0}; output buffers {outputStatus.SubmittedBufferCount:N0}; drops {outputStatus.DroppedBufferCount:N0}; underruns {outputStatus.UnderrunCount:N0}; render {FormatDuration(outputStatus.LastRenderDuration)}; jitter {FormatDuration(outputStatus.LastCallbackJitter)}.",
            $"ASIO readiness: {_asioReadinessSnapshot.Message} Drivers reported {_asioReadinessSnapshot.DriverNames.Count}; M-Audio match {(_asioReadinessSnapshot.MTrackDriverVisible ? "yes" : "no")}; channel {(_asioReadinessSnapshot.SelectedOutputChannel is null ? "none" : _asioReadinessSnapshot.SelectedOutputChannel)}; armed {_asioReadinessSnapshot.IsArmed}; Windows sound output proves ASIO {_asioReadinessSnapshot.WindowsSoundOutputVisibilityProvesAsio}."
        };

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Diagnostics" })
        {
            PageStatusText.Text = DiagnosticsSummaryText.Text;
        }
    }

    private async Task RefreshAsioVisibilityDiagnosticsAsync()
    {
        _asioVisibilitySnapshot = await _asioVisibilityDiagnostics.RefreshAsync();
        UpdateAsioDriverSelectionItems();
        await RefreshAsioReadinessDiagnosticsAsync();
        UpdateDeviceStatus();
        UpdateDiagnosticsStatus();
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

    private void TelemetryStatusTimer_Tick(object? sender, EventArgs e)
    {
        UpdateTelemetryStatus();
        UpdateHapticsStateText();
        UpdateMixerStatus();
        UpdateOutputStatus(_hapticPipeline.GetSnapshot().Output);
    }

    private void TelemetryReceiver_PacketReceived(object? sender, UdpTelemetryPacketReceivedEventArgs e)
    {
        _ = HandleLiveTelemetryPacketAsync(e.Packet);
    }

    private async Task HandleLiveTelemetryPacketAsync(UdpTelemetryPacket packet)
    {
        try
        {
            var result = await _hapticPipeline.OfferLiveTelemetryPacketAsync(packet);
            if (result.RecordingStatus == TelemetryRecordingOperationStatus.Failure)
            {
                _recordingError = result.Message;
            }
        }
        catch (Exception ex)
        {
            _recordingError ??= $"Telemetry pipeline error: {ex.Message}";
        }

        await Dispatcher.InvokeAsync(UpdateTelemetryStatus);
    }

    private void UpdateTelemetryStatus()
    {
        if (_telemetryStartError is not null)
        {
            TelemetryStatusText.Text = "UDP: unavailable";
            UdpListenerValueText.Text = "Unavailable";
            UdpListenerDetailText.Text = _telemetryStartError;
            PacketCountValueText.Text = "0";
            PacketRateDetailText.Text = "0.00 packets/s";
            UpdateForwardingStatus();
            UpdateHeaderParserStatus();
            UpdateVehicleStateStatus();
            UpdateEffectStatus();
            UpdateRecordingStatus();
            UpdateDiagnosticsStatus();
            return;
        }

        var snapshot = _telemetryReceiver.GetSnapshot();
        var status = snapshot.HasNoPacketWarning
            ? "No packets yet"
            : snapshot.IsRunning
                ? "Listening"
                : "Stopped";

        TelemetryStatusText.Text = $"UDP: {status}";
        UdpListenerValueText.Text = snapshot.IsRunning
            ? $"Listening {snapshot.BoundPort}"
            : "Stopped";
        UdpListenerDetailText.Text = snapshot.LastPacketAtUtc is null
            ? $"Default port {UdpTelemetryReceiverOptions.DefaultPort}; waiting for packets."
            : $"Last packet {snapshot.TimeSinceLastPacket?.TotalSeconds:0.0}s ago.";
        PacketCountValueText.Text = snapshot.PacketCount.ToString("N0");
        PacketRateDetailText.Text = $"{snapshot.PacketRatePerSecond:0.00} packets/s";
        UpdateForwardingStatus();
        UpdateHeaderParserStatus();
        UpdateVehicleStateStatus();
        UpdateEffectStatus();
        UpdateRecordingStatus();
        UpdateDiagnosticsStatus();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Telemetry / UDP Router" })
        {
            var pipelineSnapshot = _hapticPipeline.GetSnapshot();
            var forwardingSnapshot = pipelineSnapshot.Forwarding;
            var parsedPackets = pipelineSnapshot.ParserSuccessCount;
            var vehicleStateUpdates = pipelineSnapshot.VehicleStateUpdateCount;
            var recordingSnapshot = pipelineSnapshot.Recording;
            PageStatusText.Text = $"{status} on port {snapshot.BoundPort}; forwarding {forwardingSnapshot.ForwardedDatagramCount:N0} datagrams; recording {recordingSnapshot.PacketCount:N0} packets; parsed {parsedPackets:N0} packets; VehicleState {vehicleStateUpdates:N0} updates";
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Recordings" })
        {
            var recordingSnapshot = _hapticPipeline.GetSnapshot().Recording;
            PageStatusText.Text = recordingSnapshot.IsRecording
                ? $"Recording {recordingSnapshot.PacketCount:N0} raw packets to {Path.GetFileName(recordingSnapshot.FilePath)}"
                : "Recording idle; latest replay can feed the same mock haptic pipeline.";
        }
    }

    private void UpdateForwardingStatus()
    {
        var snapshot = _hapticPipeline.GetSnapshot().Forwarding;

        ForwardingValueText.Text = snapshot.IsEnabled
            ? $"{snapshot.EnabledDestinationCount} enabled"
            : "Disabled";
        ForwardingDetailText.Text = snapshot.IsEnabled
            ? $"{snapshot.ForwardedDatagramCount:N0} datagrams, {snapshot.ForwardedByteCount:N0} bytes."
            : $"{snapshot.DestinationCount} destinations configured; {snapshot.InputPacketCount:N0} packets observed.";
    }

    private void UpdateHeaderParserStatus()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var successCount = pipelineSnapshot.ParserSuccessCount;
        var ignoredCount = pipelineSnapshot.ParserIgnoredCount;
        var failureCount = pipelineSnapshot.ParserFailureCount;

        HeaderParserValueText.Text = successCount == 0 && ignoredCount == 0 && failureCount == 0
            ? "Waiting"
            : $"{successCount:N0} valid";
        HeaderParserDetailText.Text = successCount == 0 && ignoredCount == 0 && failureCount == 0
            ? "Validates headers and parses Stage 07 packet bodies."
            : $"Ignored {ignoredCount:N0}, failed {failureCount:N0}. {pipelineSnapshot.LastPacketMessage}";
    }

    private void UpdateVehicleStateStatus()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var updateCount = pipelineSnapshot.VehicleStateUpdateCount;
        var state = pipelineSnapshot.VehicleState;

        VehicleStateValueText.Text = updateCount == 0
            ? "Waiting"
            : $"{updateCount:N0} updates";

        if (state.Telemetry is not null)
        {
            VehicleStateDetailText.Text = $"Player {state.Frame.PlayerCarIndex}, {state.Telemetry.Value.SpeedKph} km/h, gear {state.Telemetry.Value.Gear}.";
            return;
        }

        VehicleStateDetailText.Text = updateCount == 0
            ? "Maps parsed Stage 07 packet bodies into shared VehicleState samples."
            : pipelineSnapshot.LastVehicleStateMessage;
    }

    private void UpdateEffectStatus()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var snapshot = pipelineSnapshot.Effects;
        var options = _hapticPipeline.EffectEngine.Options;

        EngineEffectStateText.Text = snapshot.Engine.IsActive ? "Active" : "Idle";
        EngineEffectDetailText.Text = snapshot.Engine.LastRpm is null
            ? "Waiting for RPM telemetry."
            : $"{snapshot.Engine.LastRpm:N0} RPM -> {snapshot.Engine.CurrentFrequencyHz:0.0} Hz, peak {snapshot.Engine.PeakLevel:0.000}.";
        EngineEffectDefaultsText.Text = $"Tuned gain {options.Engine.Gain:P0}; base {options.Engine.MinimumFrequencyHz:0}-{options.Engine.MaximumFrequencyHz:0} Hz; enabled {options.Engine.IsEnabled}.";

        GearShiftEffectStateText.Text = snapshot.GearShift.IsActive ? "Pulse active" : "Idle";
        GearShiftEffectDetailText.Text = snapshot.GearShift.LastObservedGear is null
            ? "Waiting for gear telemetry."
            : $"Last gear {snapshot.GearShift.LastObservedGear}; last shift frame {snapshot.GearShift.LastShiftFrameIdentifier?.ToString("N0") ?? "none"}; peak {snapshot.GearShift.PeakLevel:0.000}.";
        GearShiftEffectDefaultsText.Text = $"Tuned gain {options.GearShift.Gain:P0}; {options.GearShift.PulseFrequencyHz:0} Hz pulse; {options.GearShift.PulseDuration.TotalMilliseconds:0} ms; enabled {options.GearShift.IsEnabled}.";

        KerbEffectStateText.Text = snapshot.Kerb.IsActive ? "Active" : "Idle";
        KerbEffectDetailText.Text = snapshot.Kerb.DominantSurfaceTypeId is null
            ? "Waiting for rumble strip / ridged surface telemetry."
            : $"{snapshot.Kerb.DominantSurfaceName}; {snapshot.Kerb.ActiveWheelCount} wheel(s); {snapshot.Kerb.CurrentFrequencyHz:0.0} Hz; peak {snapshot.Kerb.PeakLevel:0.000}.";
        KerbEffectDefaultsText.Text = $"Tuned gain {options.Kerb.Gain:P0}; {options.Kerb.BaseFrequencyHz:0} Hz + {options.Kerb.HighFrequencyHz:0} Hz; enabled {options.Kerb.IsEnabled}.";

        ImpactEffectStateText.Text = snapshot.Impact.IsActive ? "Pulse active" : "Idle";
        ImpactEffectDetailText.Text = snapshot.Impact.LastImpactFrameIdentifier is null
            ? "Waiting for collision, vertical-G, force, or suspension spikes."
            : $"Last impact frame {snapshot.Impact.LastImpactFrameIdentifier:N0}; intensity {snapshot.Impact.CurrentIntensity:0.00}; peak {snapshot.Impact.PeakLevel:0.000}.";
        ImpactEffectDefaultsText.Text = $"Tuned gain {options.Impact.Gain:P0}; {options.Impact.PulseFrequencyHz:0} Hz; {options.Impact.PulseDuration.TotalMilliseconds:0} ms; enabled {options.Impact.IsEnabled}.";

        RoadTextureEffectStateText.Text = snapshot.RoadTexture.IsActive ? "Active" : "Idle";
        RoadTextureEffectDetailText.Text = snapshot.RoadTexture.DominantSurfaceTypeId is null
            ? "Waiting for speed and surface telemetry."
            : $"{snapshot.RoadTexture.DominantSurfaceName}; mix {snapshot.RoadTexture.SurfaceMix:0.00}; {snapshot.RoadTexture.CurrentFrequencyHz:0.0} Hz; peak {snapshot.RoadTexture.PeakLevel:0.000}.";
        RoadTextureEffectDefaultsText.Text = $"Tuned gain {options.RoadTexture.Gain:P0}; {options.RoadTexture.MinimumSpeedKph:0}-{options.RoadTexture.FullIntensitySpeedKph:0} km/h; enabled {options.RoadTexture.IsEnabled}.";

        SlipEffectStateText.Text = snapshot.Slip.IsActive ? "Active" : "Idle";
        SlipEffectDetailText.Text = snapshot.Slip.CurrentSlipIntensity <= 0f && snapshot.Slip.CurrentLockIntensity <= 0f
            ? "Waiting for Motion Ex slip ratio / angle telemetry."
            : $"Slip {snapshot.Slip.CurrentSlipIntensity:0.00}; lock {snapshot.Slip.CurrentLockIntensity:0.00}; {snapshot.Slip.CurrentFrequencyHz:0.0} Hz; peak {snapshot.Slip.PeakLevel:0.000}.";
        SlipEffectDefaultsText.Text = $"Tuned gain {options.Slip.Gain:P0}; {options.Slip.BaseFrequencyHz:0} Hz slip; threshold {options.Slip.SlipRatioThreshold:0.00}; enabled {options.Slip.IsEnabled}.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Effects" })
        {
            PageStatusText.Text = $"{snapshot.ActiveEffectCount} active effect source(s); engine {EngineEffectStateText.Text.ToLowerInvariant()}, gear {GearShiftEffectStateText.Text.ToLowerInvariant()}, kerb {KerbEffectStateText.Text.ToLowerInvariant()}, impact {ImpactEffectStateText.Text.ToLowerInvariant()}, road {RoadTextureEffectStateText.Text.ToLowerInvariant()}, slip {SlipEffectStateText.Text.ToLowerInvariant()}; peak {snapshot.PeakLevel:0.000}.";
        }

        UpdateDiagnosticsStatus();
    }

    private void UpdateRecordingStatus()
    {
        var snapshot = _hapticPipeline.GetSnapshot().Recording;
        var replaySnapshot = _hapticPipeline.GetSnapshot().Replay;
        var buttonText = snapshot.IsRecording ? "Stop Recording" : "Start Recording";
        StartRecordingButton.Content = buttonText;
        RecordingsStartStopButton.Content = buttonText;
        ReplayStartStopButton.Content = replaySnapshot.IsReplaying ? "Stop Replay" : "Replay Latest";

        if (_recordingError is not null)
        {
            RecordingValueText.Text = "Error";
            RecordingDetailText.Text = _recordingError;
            RecordingsDetailText.Text = _recordingError;
            return;
        }

        RecordingValueText.Text = snapshot.IsRecording
            ? $"{snapshot.PacketCount:N0} packets"
            : "Idle";
        if (snapshot.IsRecording)
        {
            RecordingDetailText.Text = snapshot.LastPacketRelativeTime is null
                ? $"Writing {Path.GetFileName(snapshot.FilePath)}; waiting for first packet."
                : $"Writing {Path.GetFileName(snapshot.FilePath)}; last packet {snapshot.LastPacketRelativeTime.Value.TotalSeconds:0.000}s.";
            RecordingsDetailText.Text = RecordingDetailText.Text;
            ReplayDetailText.Text = BuildReplayStatusText();
            return;
        }

        RecordingDetailText.Text = "Ready to capture raw UDP packets to versioned replay files.";
        RecordingsDetailText.Text = RecordingDetailText.Text;
        ReplayDetailText.Text = BuildReplayStatusText();
        UpdateDiagnosticsStatus();
    }

    private string BuildReplayStatusText()
    {
        var snapshot = _hapticPipeline.GetSnapshot().Replay;
        if (_replayError is not null)
        {
            return _replayError;
        }

        return snapshot.IsReplaying
            ? $"Replay active from {Path.GetFileName(snapshot.SourceFilePath)}; {snapshot.PacketsReplayed:N0} packet(s)."
            : $"Replay inactive; {snapshot.PacketsReplayed:N0} packet(s) last replayed. {snapshot.StatusMessage}";
    }

    private static string CreateDefaultRecordingPath()
    {
        var recordingsDirectory = GetRecordingsDirectory();
        Directory.CreateDirectory(recordingsDirectory);

        return Path.Combine(
            recordingsDirectory,
            $"f1-25-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.hdrec");
    }

    private static string? FindLatestRecordingPath()
    {
        var recordingsDirectory = GetRecordingsDirectory();
        if (!Directory.Exists(recordingsDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(recordingsDirectory, "*.hdrec", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string GetRecordingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HapticDrive.Asio",
            "Recordings");
    }

    protected override void OnClosed(EventArgs e)
    {
        _telemetryStatusTimer.Stop();
        _testBench.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _telemetryReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _hapticPipeline.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnClosed(e);
    }

    private sealed record ShellPageDefinition(
        string NavigationLabel,
        string Title,
        string Summary,
        string Status,
        IReadOnlyList<string> Items);

    private sealed record OutputModeOption(
        AudioOutputDeviceKind Kind,
        string Label);

    private sealed record ThemePalette(
        string Background,
        string Surface,
        string SurfaceAlt,
        string Border,
        string Text,
        string MutedText,
        string Accent,
        string Danger,
        string Success);
}
