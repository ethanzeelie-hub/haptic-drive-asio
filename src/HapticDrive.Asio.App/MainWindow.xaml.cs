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
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Input.Windows;
using System.IO;
using System.Net;
using System.Text;
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
    private readonly AppSettingsStore _settingsStore = new();
    private readonly AudioTestBench _testBench = new();
    private readonly IInputDeviceDiscovery _inputDeviceDiscovery = new WindowsInputDeviceDiscovery();
    private readonly IWheelInputCandidateProvider _wheelInputCandidateProvider = new WheelInputCandidateProvider();
    private readonly IWheelPaddleInputSource _paddleInputSource = new PollingWheelPaddleInputSource(
        new WindowsGameControllerButtonStateReader());
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
            "Stage 18 final pre-shaker readiness pipeline",
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
                "Stage 18 keeps live UDP and replayed packets wired into the parser, VehicleState adapter, effects, mixer, safety chain, and output-owned renderer.",
                "Stage 2E adds read-only Windows game-controller paddle input diagnostics and manual left/right mapping.",
                "Stale telemetry is muted by wall-clock timeout.",
                "NullAudioOutputDevice is the default safe output.",
                "The app remains safe to open without ASIO hardware or shaker hardware."
            ]),
        new(
            "Effects",
            "Effects",
            "Hardware-safe generated effect diagnostics.",
            "Stage 18 effect tuning drives the output-owned render path",
            [
                "Gear shift is synthesized from valid forward gear changes in VehicleState telemetry.",
                "Engine vibration is synthesized from RPM, throttle, idle RPM, max RPM, gear, speed, and pause/status gates where available.",
                "Kerb vibration is synthesized from rumble strip / ridged surface IDs, speed, and optional suspension/contact data.",
                "Impact pulses are synthesized from player collision events and abrupt vertical-G, wheel-force, or suspension-acceleration spikes.",
                "Road texture is synthesized from surface IDs, speed, and optional suspension / vertical-G motion.",
                "Slip and minimal brake-lock vibration are synthesized from wheel slip ratio/angle, wheel speed, throttle, brake, speed, TC, and ABS fields.",
                "Defaults are conservative and inspired by SimHub-style frequency, gain, pulse-duration, speed, and threshold controls.",
                "Live haptics render through an output-owned callback path; the test bench remains deterministic validation only.",
                "Advanced live graphs, routing matrices, and physical calibration wait for post-BT-1 hardware stages."
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
            "Safe status for output abstractions and read-only input discovery.",
            "Output and input discovery status available",
            [
                "NullAudioOutputDevice is available for automated tests and safe app startup.",
                "WasapiDebugOutputDevice exists as a manual debug placeholder only.",
                "AsioAudioOutputDevice exists behind the same interface and fails gracefully when no driver is available.",
                "Refresh Input Devices performs read-only Raw Input and Windows game-controller discovery.",
                "Live paddle diagnostics read Windows game-controller button states only after explicit Start Listener.",
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
                "Forwarding destinations can be edited and persisted from this page.",
                "Packet-ID diagnostics and copyable reports help verify telemetry flow before shaker hardware arrives."
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
                "The recordings library lists local .hdrec files and can replay the selected recording."
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
                "Stage 18 reports pipeline, engine, gear, kerb, impact, road texture, slip, mixer, safety, output, replay, forwarding, packet-ID, ASIO visibility, callback, drop, underrun, jitter, and telemetry-age state.",
                "Stage 2E reports read-only input discovery, selected paddle device, mapping, last raw button, and mapped paddle press diagnostics.",
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
    private string? _settingsError;
    private HapticDriveProfile _currentProfile = HapticDriveProfile.Default;
    private HapticPipelineCoordinator _hapticPipeline;
    private AsioDriverVisibilitySnapshot _asioVisibilitySnapshot = AsioDriverVisibilitySnapshot.NotChecked;
    private AsioReadinessSnapshot _asioReadinessSnapshot = AsioReadinessSnapshot.NotChecked;
    private InputDeviceDiscoverySnapshot _inputDiscoverySnapshot = InputDeviceDiscoverySnapshot.NotRun;
    private WheelPaddleMapping _paddleMapping = WheelPaddleMapping.Default;
    private Task? _activeReplayTask;
    private bool _updatingTuningUi;
    private bool _updatingSettingsUi;
    private bool _updatingPaddleInputUi;
    private List<ForwardingDestinationSetting> _forwardingDestinations = [];
    private List<ForwardingDestinationListItem> _forwardingDestinationItems = [];
    private List<PaddleDeviceListItem> _paddleDeviceItems = [];
    private List<RecordingLibraryItem> _recordingLibraryItems = [];

    public MainWindow()
    {
        _asioVisibilityDiagnostics = new AsioDriverVisibilityDiagnostics(_asioDriverCatalog);
        _asioReadinessDiagnostics = new AsioReadinessDiagnostics(_asioDriverCatalog);

        var appSettings = _settingsStore.Load();
        _settingsError = appSettings.LastStatusMessage;
        _lightTheme = appSettings.UseLightTheme;
        _selectedAsioDriverName = appSettings.LastAsioDriverName;
        _selectedAsioOutputChannel = appSettings.LastAsioOutputChannel;
        _forwardingDestinations = appSettings.ForwardingDestinations.ToList();
        _paddleMapping = CreatePaddleMapping(appSettings.PaddleInputMapping);
        _hapticPipeline = CreatePipelineForSelectedOutput();

        _updatingOutputUi = true;
        _updatingTuningUi = true;
        _updatingSettingsUi = true;
        InitializeComponent();

        NavigationList.ItemsSource = _pages;
        NavigationList.SelectedIndex = 0;
        OutputModeComboBox.ItemsSource = _outputModeOptions;
        OutputModeComboBox.SelectedItem = _outputModeOptions.Single(option => option.Kind == _selectedOutputKind);
        AsioOutputChannelComboBox.ItemsSource = _asioOutputChannelChoices;
        AsioOutputChannelComboBox.SelectedItem = _selectedAsioOutputChannel;
        _updatingOutputUi = false;
        _updatingSettingsUi = false;
        TestBenchSignalComboBox.ItemsSource = _testBenchSignals;
        TestBenchSignalComboBox.SelectedIndex = 1;
        ApplyTheme(_lightTheme);
        RefreshForwardingDestinationItems();
        ApplyProfileToControls(_currentProfile);
        ApplyProfileToRuntime(_currentProfile);
        UpdateProfileStatus("Default conservative profile loaded.", []);
        ApplyPaddleMappingToControls();
        _paddleInputSource.RawButtonChanged += PaddleInputSource_InputChanged;
        _paddleInputSource.PaddleInputReceived += PaddleInputSource_InputChanged;
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
        UpdateForwardingEditorStatus();
        await RefreshRecordingLibraryAsync();
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
            ForwardingPanel.Visibility = page.NavigationLabel == "Telemetry / UDP Router"
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
            FooterStatusText.Text = $"Viewing {page.NavigationLabel} - Stage 2E read-only paddle input diagnostics";
            UpdateTelemetryStatus();
            UpdateEffectStatus();
            UpdateMixerStatus();
            UpdateDeviceStatus();
            UpdateProfileStatus();
            UpdateTestBenchStatus();
            UpdateDiagnosticsStatus();
            if (page.NavigationLabel == "Recordings")
            {
                _ = RefreshRecordingLibraryAsync();
            }
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
        SaveAppSettings();
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
        SaveAppSettings();
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

    private async void RefreshInputDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshInputDeviceDiscoveryAsync();
    }

    private async Task RefreshInputDeviceDiscoveryAsync()
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
            FooterStatusText.Text = $"Input device discovery refreshed; {_inputDiscoverySnapshot.DeviceCount:N0} device(s) found. No commands were sent.";
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
        if (!TryBuildPaddleMappingFromControls(out var mapping, out var message))
        {
            PaddleInputStatusText.Text = message;
            FooterStatusText.Text = message;
            return;
        }

        if (PaddleInputDeviceComboBox.SelectedItem is not PaddleDeviceListItem deviceItem)
        {
            PaddleInputStatusText.Text = "Select a Windows game-controller input device before starting the read-only paddle listener.";
            FooterStatusText.Text = "Paddle input listener was not started; no input device is selected.";
            return;
        }

        _paddleMapping = mapping with
        {
            SelectedDeviceId = deviceItem.Selection.DeviceId,
            SelectedMethod = deviceItem.Selection.Method
        };
        SaveAppSettings();
        await _paddleInputSource.StartAsync(deviceItem.Selection, _paddleMapping);
        UpdatePaddleInputStatus();
        UpdateDiagnosticsStatus();
        FooterStatusText.Text = "Read-only paddle input listener started. Mapped presses update diagnostics only.";
    }

    private async void StopPaddleInputListenerButton_Click(object sender, RoutedEventArgs e)
    {
        await _paddleInputSource.StopAsync();
        UpdatePaddleInputStatus();
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
            profile: _currentProfile,
            forwardingDestinations: CreateForwardingDestinations());
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
        await RebuildHapticPipelineForOutputSelectionAsync("UDP forwarding destinations updated; haptics are stopped until started explicitly.");
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
        await RebuildHapticPipelineForOutputSelectionAsync($"Removed UDP forwarding destination {removed.Name}; haptics are stopped until started explicitly.");
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
        setting = new ForwardingDestinationSetting();
        var host = ForwardingHostTextBox.Text.Trim();
        var name = ForwardingNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            message = "Forwarding host or IP address is required.";
            return false;
        }

        if (!int.TryParse(ForwardingPortTextBox.Text.Trim(), out var port) || port is < 1 or > 65_535)
        {
            message = "Forwarding port must be between 1 and 65535.";
            return false;
        }

        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            message = "Forwarding host must be a valid DNS name, localhost, IPv4 address, or IPv6 address.";
            return false;
        }

        var enabled = ForwardingEnabledCheckBox.IsChecked == true;
        if (enabled && IsObviousUdpLoopback(host, port))
        {
            message = $"Forwarding to {host}:{port} would loop back to the local listener port and is blocked.";
            return false;
        }

        setting = new ForwardingDestinationSetting
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{host}:{port}" : name,
            Host = host,
            Port = port,
            Enabled = enabled
        };
        message = "Forwarding destination ready.";
        return true;
    }

    private static bool IsObviousUdpLoopback(string host, int port)
    {
        if (port != UdpTelemetryReceiverOptions.DefaultPort)
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address)
            && IPAddress.IsLoopback(address);
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
        var enabled = _forwardingDestinations.Count(destination => destination.Enabled);
        ForwardingDestinationsSummaryText.Text = _forwardingDestinations.Count == 0
            ? "No forwarding destinations configured. Recording and parsing still work normally."
            : $"{enabled}/{_forwardingDestinations.Count} destination(s) enabled. Loopback to UDP {UdpTelemetryReceiverOptions.DefaultPort} is blocked.";

        if (string.IsNullOrWhiteSpace(ForwardingEditorStatusText.Text))
        {
            ForwardingEditorStatusText.Text = "Use 127.0.0.1 or localhost for local tools; choose a port other than the listener port.";
        }
    }

    private void UpdatePaddleDeviceSelectionItems()
    {
        var devices = _inputDiscoverySnapshot.HasRun
            ? _inputDiscoverySnapshot.Devices
                .Where(device => device.DiscoveryMethod == InputDiscoveryMethod.WindowsGameController
                    && device.Kind == InputDeviceKind.GameController
                    && device.NativeDeviceIndex is not null)
                .OrderByDescending(device => device.LooksLikeGtNeoOrWheelInput)
                .ThenByDescending(device => device.CandidateScore)
                .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        _paddleDeviceItems = devices
            .Select(device => new PaddleDeviceListItem(
                InputDeviceSelection.FromDeviceInfo(device),
                $"{device.DisplayName} - {device.ButtonCount?.ToString() ?? "unknown"} button(s) - {device.CandidateKind}"))
            .ToList();

        _updatingPaddleInputUi = true;
        PaddleInputDeviceComboBox.ItemsSource = _paddleDeviceItems;
        PaddleInputDeviceComboBox.DisplayMemberPath = nameof(PaddleDeviceListItem.DisplayText);

        var selected = _paddleDeviceItems.FirstOrDefault(item =>
            string.Equals(item.Selection.DeviceId, _paddleMapping.SelectedDeviceId, StringComparison.OrdinalIgnoreCase));
        PaddleInputDeviceComboBox.SelectedItem = selected;
        _updatingPaddleInputUi = false;
    }

    private void ApplyPaddleMappingToControls()
    {
        _updatingPaddleInputUi = true;
        LeftPaddleButtonTextBox.Text = _paddleMapping.LeftPaddleButtonId?.ToString() ?? "";
        RightPaddleButtonTextBox.Text = _paddleMapping.RightPaddleButtonId?.ToString() ?? "";
        PaddleDebounceTextBox.Text = ((int)_paddleMapping.DebounceDuration.TotalMilliseconds).ToString();
        _updatingPaddleInputUi = false;
        UpdatePaddleDeviceSelectionItems();
        UpdatePaddleInputStatus();
    }

    private bool TryBuildPaddleMappingFromControls(out WheelPaddleMapping mapping, out string message)
    {
        mapping = _paddleMapping;
        if (!TryParseOptionalButtonId(LeftPaddleButtonTextBox.Text, out var leftButtonId, out message))
        {
            return false;
        }

        if (!TryParseOptionalButtonId(RightPaddleButtonTextBox.Text, out var rightButtonId, out message))
        {
            return false;
        }

        if (!int.TryParse(PaddleDebounceTextBox.Text.Trim(), out var debounceMilliseconds)
            || debounceMilliseconds is < 0 or > 250)
        {
            message = "Paddle debounce must be between 0 and 250 ms.";
            return false;
        }

        mapping = new WheelPaddleMapping
        {
            SelectedDeviceId = (PaddleInputDeviceComboBox.SelectedItem as PaddleDeviceListItem)?.Selection.DeviceId
                ?? _paddleMapping.SelectedDeviceId,
            SelectedMethod = InputDiscoveryMethod.WindowsGameController,
            LeftPaddleButtonId = leftButtonId,
            RightPaddleButtonId = rightButtonId,
            DebounceDuration = TimeSpan.FromMilliseconds(debounceMilliseconds)
        }.Normalize();
        message = "Paddle input mapping ready.";
        return true;
    }

    private static bool TryParseOptionalButtonId(string text, out int? buttonId, out string message)
    {
        buttonId = null;
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            message = "Button is unmapped.";
            return true;
        }

        if (!int.TryParse(trimmed, out var parsed) || parsed is < 1 or > 128)
        {
            message = "Paddle button IDs must be whole numbers from 1 to 128.";
            return false;
        }

        buttonId = parsed;
        message = "Button mapped.";
        return true;
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

    private static WheelPaddleMapping CreatePaddleMapping(PaddleInputMappingSetting setting)
    {
        return new WheelPaddleMapping
        {
            SelectedDeviceId = setting.SelectedDeviceId,
            SelectedMethod = setting.SelectedMethod,
            LeftPaddleButtonId = setting.LeftPaddleButtonId,
            RightPaddleButtonId = setting.RightPaddleButtonId,
            DebounceDuration = TimeSpan.FromMilliseconds(setting.DebounceMilliseconds)
        }.Normalize();
    }

    private PaddleInputMappingSetting CreatePaddleMappingSetting()
    {
        var mapping = _paddleMapping.Normalize();
        return new PaddleInputMappingSetting
        {
            SelectedDeviceId = mapping.SelectedDeviceId,
            SelectedMethod = mapping.SelectedMethod,
            LeftPaddleButtonId = mapping.LeftPaddleButtonId,
            RightPaddleButtonId = mapping.RightPaddleButtonId,
            DebounceMilliseconds = (int)mapping.DebounceDuration.TotalMilliseconds
        };
    }

    private void SaveAppSettings()
    {
        try
        {
            _settingsStore.Save(new AppSettings
            {
                UseLightTheme = _lightTheme,
                LastAsioDriverName = _selectedAsioDriverName,
                LastAsioOutputChannel = _selectedAsioOutputChannel,
                ForwardingDestinations = _forwardingDestinations.ToList(),
                PaddleInputMapping = CreatePaddleMappingSetting()
            });
            _settingsError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _settingsError = $"App settings could not be saved: {ex.Message}";
        }

        UpdateProfileStatus();
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
        _activeReplayTask = ReplayRecordingAsync(path);
        UpdateRecordingStatus();
    }

    private async void ReplaySelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            _replayError = "Select a recording from the library before replaying.";
            FooterStatusText.Text = _replayError;
            UpdateRecordingStatus();
            return;
        }

        var replaySnapshot = _hapticPipeline.GetSnapshot().Replay;
        if (replaySnapshot.IsReplaying)
        {
            await _hapticPipeline.ReplayService.StopAsync();
            FooterStatusText.Text = "Replay stop requested.";
            UpdateRecordingStatus();
            UpdateDiagnosticsStatus();
            return;
        }

        _replayError = null;
        FooterStatusText.Text = $"Replaying selected recording {Path.GetFileName(item.Path)}.";
        _activeReplayTask = ReplayRecordingAsync(item.Path);
        UpdateRecordingStatus();
    }

    private async void RefreshRecordingsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRecordingLibraryAsync();
    }

    private void RecordingLibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem item)
        {
            RecordingLibraryStatusText.Text = item.DetailText;
        }
    }

    private async Task ReplayRecordingAsync(string path)
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

    private async Task RefreshRecordingLibraryAsync()
    {
        try
        {
            _recordingLibraryItems = await LoadRecordingLibraryItemsAsync();
            RecordingLibraryListBox.ItemsSource = _recordingLibraryItems;
            RecordingLibraryStatusText.Text = _recordingLibraryItems.Count == 0
                ? $"No .hdrec files found in {GetRecordingsDirectory()}."
                : $"{_recordingLibraryItems.Count} recording(s) found in {GetRecordingsDirectory()}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            RecordingLibraryStatusText.Text = $"Recording library could not be refreshed: {ex.Message}";
        }
    }

    private static async Task<List<RecordingLibraryItem>> LoadRecordingLibraryItemsAsync()
    {
        var recordingsDirectory = GetRecordingsDirectory();
        if (!Directory.Exists(recordingsDirectory))
        {
            return [];
        }

        var items = new List<RecordingLibraryItem>();
        foreach (var path in Directory
            .EnumerateFiles(recordingsDirectory, "*.hdrec", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var result = await TelemetryRecordingFile.LoadSummaryAsync(path).ConfigureAwait(false);
            if (result.Succeeded && result.Summary is not null)
            {
                var summary = result.Summary;
                var sizeText = summary.FileSizeBytes >= 1024 * 1024
                    ? $"{summary.FileSizeBytes / 1024d / 1024d:0.0} MB"
                    : $"{summary.FileSizeBytes / 1024d:0.0} KB";
                var createdLocal = summary.Metadata.CreatedAtUtc.ToLocalTime();
                items.Add(new RecordingLibraryItem(
                    path,
                    $"{Path.GetFileName(path)} - {summary.PacketCount:N0} packet(s) - {sizeText}",
                    $"Created {createdLocal:g}; source {summary.Metadata.SourceGame}; profile {summary.Metadata.SourceProfile}; app {summary.Metadata.AppVersion}; modified {summary.LastModifiedAtUtc.ToLocalTime():g}."));
                continue;
            }

            items.Add(new RecordingLibraryItem(
                path,
                $"{Path.GetFileName(path)} - {result.Status}",
                result.Message));
        }

        return items;
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
        SaveAppSettings();
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
        SaveAppSettings();
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

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateDiagnosticsStatus();
        var report = new StringBuilder()
            .AppendLine("Haptic Drive ASIO diagnostics")
            .AppendLine($"Generated: {DateTimeOffset.Now:g}")
            .AppendLine(DiagnosticsSummaryText.Text);

        if (DiagnosticsItemsControl.ItemsSource is IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                report.AppendLine(item);
            }
        }

        try
        {
            Clipboard.SetText(report.ToString());
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
        UpdateInputDiscoveryStatus();
        UpdatePaddleInputStatus();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Devices" })
        {
            PageStatusText.Text = status.RequiresPhysicalHardware
                ? "Selected output requires explicit manual hardware readiness checks; haptics remain stopped until armed and started."
                : $"Hardware-absent mode active; NullAudioOutputDevice remains the safe default; ASIO drivers reported {_asioVisibilitySnapshot.DriverNames.Count}; input devices discovered {(_inputDiscoverySnapshot.HasRun ? _inputDiscoverySnapshot.DeviceCount.ToString("N0") : "not refreshed")}; paddle listener {_paddleInputSource.GetPaddleSnapshot().Status}.";
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
        SettingsStatusText.Text = $"Theme: {(_lightTheme ? "Light" : "Dark")}. Active profile: {_currentProfile.Name}. Forwarding destinations {_forwardingDestinations.Count}. Paddle mapping left {FormatButtonMapping(_paddleMapping.LeftPaddleButtonId)}, right {FormatButtonMapping(_paddleMapping.RightPaddleButtonId)}. Default output remains NullAudioOutputDevice. {_settingsError ?? ""}".Trim();
        SettingsPathText.Text = $"App settings path: {_settingsStore.SettingsPath}";
        RuntimePrerequisiteText.Text = $".NET Desktop runtime is available for this running WPF app. Launch script sets DOTNET_ROOT to the repo-local .NET 8 runtime before starting the executable.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Profiles" })
        {
            PageStatusText.Text = $"Active profile {_currentProfile.Name}; JSON version {HapticDriveProfile.CurrentVersion}; emergency mute is not saved.";
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Settings" })
        {
            PageStatusText.Text = $"Theme {(_lightTheme ? "light" : "dark")}; app settings are local; ASIO armed state and auto-start are never persisted.";
        }
    }

    private void UpdateInputDiscoveryStatus()
    {
        if (!_inputDiscoverySnapshot.HasRun)
        {
            InputDiscoveryStatusText.Text = "Input discovery has not been refreshed. Use Refresh Input Devices to enumerate read-only Windows input metadata.";
            InputDiscoveryItemsControl.ItemsSource = new[]
            {
                "Safety: discovery refresh only. The live listener starts only from Start Listener and never routes haptics or P-HPR commands.",
                "Selected input device: none. Use Refresh Input Devices, select a Windows game-controller device, then start the listener for diagnostics."
            };
            return;
        }

        var snapshot = _inputDiscoverySnapshot;
        var localRefreshTime = snapshot.DiscoveredAtUtc.ToLocalTime().ToString("g");
        var methodText = FormatDiscoveryMethods(snapshot.Methods);
        var errorText = snapshot.Errors.Count == 0 ? "none" : string.Join("; ", snapshot.Errors);
        var candidates = _wheelInputCandidateProvider.GetCandidates(snapshot);

        InputDiscoveryStatusText.Text =
            $"Input discovery: {(snapshot.ReadOnlyDiscoverySucceeded ? "succeeded" : "completed with warnings")}; refreshed {localRefreshTime}; {snapshot.DeviceCount:N0} device(s); methods {methodText}; errors {snapshot.Errors.Count:N0}.";
        InputDiscoveryItemsControl.ItemsSource = new[]
        {
            "Safety: read-only discovery and read-only button-state diagnostics only. No haptic routing, USB output report, feature report, or P-HPR output command is used.",
            $"Candidate devices: {FormatInputCandidates(candidates)}",
            $"Likely Simagic wheelbase candidates: {FormatInputCandidates(snapshot.LikelySimagicWheelBaseCandidates)}",
            $"Likely GT Neo / wheel input candidates: {FormatInputCandidates(snapshot.LikelyGtNeoWheelInputCandidates)}",
            $"Likely P700 pedal candidates: {FormatInputCandidates(snapshot.LikelyP700PedalCandidates)}",
            $"Unknown HID/game-controller candidates: {FormatInputCandidates(snapshot.UnknownHidOrGameControllerCandidates)}",
            "Manual mapping: start the listener, press a paddle, then set left/right from the last changed button.",
            $"Discovery errors: {errorText}"
        };
    }

    private void UpdatePaddleInputStatus()
    {
        var snapshot = _paddleInputSource.GetPaddleSnapshot();
        var selectedText = snapshot.SelectedDevice is null
            ? _paddleMapping.SelectedDeviceId is null ? "none" : $"saved {_paddleMapping.SelectedDeviceId}"
            : $"{snapshot.SelectedDevice.DisplayName} ({snapshot.SelectedDevice.Method})";
        var lastRaw = snapshot.LastChangedButtonId is null
            ? "none"
            : $"button {snapshot.LastChangedButtonId} {snapshot.LastChangedButtonState}";
        var lastMapped = snapshot.LastPaddleEvent is null
            ? "none"
            : $"{snapshot.LastPaddleEvent.PaddleSide} paddle button {snapshot.LastPaddleEvent.ButtonId} at {snapshot.LastPaddleEvent.TimestampUtc.ToLocalTime():T}";
        var error = snapshot.LastErrorMessage ?? "none";

        StartPaddleInputListenerButton.IsEnabled = snapshot.Status is not InputListenerStatus.Listening
            and not InputListenerStatus.Starting;
        StopPaddleInputListenerButton.IsEnabled = snapshot.Status is InputListenerStatus.Listening
            or InputListenerStatus.Starting
            or InputListenerStatus.Error
            or InputListenerStatus.Disconnected;
        PaddleInputStatusText.Text =
            $"Paddle listener: {snapshot.Status}; selected {selectedText}; mapped presses {snapshot.PaddlePressCount:N0}; last raw {lastRaw}; last mapped {lastMapped}; error {error}.";
        PaddleInputItemsControl.ItemsSource = new[]
        {
            "Safety: Stage 2E reads game-controller button states only. It does not route ShiftIntent, audio haptics, P-HPR output, USB output reports, or feature reports.",
            $"Selected method: {(_paddleMapping.SelectedMethod == InputDiscoveryMethod.Unknown ? InputDiscoveryMethod.WindowsGameController : _paddleMapping.SelectedMethod)}",
            $"Left paddle mapping: {FormatButtonMapping(snapshot.Mapping.LeftPaddleButtonId)}; current state {snapshot.LeftPaddleState}",
            $"Right paddle mapping: {FormatButtonMapping(snapshot.Mapping.RightPaddleButtonId)}; current state {snapshot.RightPaddleState}",
            $"Last changed raw button: {lastRaw}",
            $"Last mapped paddle event: {lastMapped}",
            $"Paddle press count: {snapshot.PaddlePressCount:N0}",
            $"Debounce: {snapshot.Mapping.DebounceDuration.TotalMilliseconds:0} ms",
            $"Listener error: {error}"
        };
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
        var packetDiagnostics = BuildPacketDiagnosticsText(pipelineSnapshot.PacketDiagnostics);

        DiagnosticsSummaryText.Text = $"UDP {receiverSnapshot.PacketCount:N0} packet(s), parser {parserSuccess:N0} valid / {parserFailed:N0} failed, effects {audioDiagnostics.ActiveEffectCount}, output peak {audioDiagnostics.OutputPeakLevel:0.000}, callbacks {outputStatus.RenderCallbackCount:N0}.";
        DiagnosticsItemsControl.ItemsSource = new[]
        {
            $"Pipeline: {(pipelineSnapshot.IsRunning ? "running" : "stopped")}; source {pipelineSnapshot.InputSource}; rendered {pipelineSnapshot.RenderedBufferCount:N0} buffer(s); telemetry age {(pipelineSnapshot.TelemetryAge is null ? "none" : $"{pipelineSnapshot.TelemetryAge.Value.TotalMilliseconds:0} ms")}; stale mute {pipelineSnapshot.TelemetryTimedOutMuted}; last error {pipelineSnapshot.LastPipelineError ?? "none"}.",
            $"UDP listener: {(receiverSnapshot.IsRunning ? "running" : "stopped")} on port {receiverSnapshot.BoundPort}; rate {receiverSnapshot.PacketRatePerSecond:0.00}/s; last packet {(receiverSnapshot.LastPacketAtUtc is null ? "never" : $"{receiverSnapshot.TimeSinceLastPacket?.TotalSeconds:0.0}s ago")}.",
            $"UDP forwarding: {forwarderSnapshot.EnabledDestinationCount}/{forwarderSnapshot.DestinationCount} destination(s) enabled; {forwarderSnapshot.ForwardedDatagramCount:N0} datagrams; {forwarderSnapshot.ErrorCount:N0} error(s).",
            $"UDP forwarding destinations: {BuildForwardingDestinationsText()}",
            $"Parser: {parserSuccess:N0} valid, {parserIgnored:N0} ignored, {parserFailed:N0} failed. {pipelineSnapshot.LastPacketMessage}",
            $"Packet IDs: {packetDiagnostics}",
            $"VehicleState: {vehicleUpdates:N0} update(s). {pipelineSnapshot.LastVehicleStateMessage}",
            $"Recording: {(recordingSnapshot.IsRecording ? "active" : "inactive")}; {recordingSnapshot.PacketCount:N0} packet(s); file {(recordingSnapshot.FilePath is null ? "none" : Path.GetFileName(recordingSnapshot.FilePath))}.",
            $"Replay: {(replaySnapshot.IsReplaying ? "active" : "inactive")}; {replaySnapshot.PacketsReplayed:N0} packet(s); {replaySnapshot.StatusMessage}",
            $"Effects: enabled engine {effectSnapshot.Engine.IsEnabled}, gear {effectSnapshot.GearShift.IsEnabled}, kerb {effectSnapshot.Kerb.IsEnabled}, impact {effectSnapshot.Impact.IsEnabled}, road {effectSnapshot.RoadTexture.IsEnabled}, slip {effectSnapshot.Slip.IsEnabled}; peak {effectSnapshot.PeakLevel:0.000}.",
            $"Mixer / safety: mixer peak {audioDiagnostics.MixerPeakLevel:0.000}; output peak {audioDiagnostics.OutputPeakLevel:0.000}; limited {audioDiagnostics.LimitedSampleCount:N0}; clipped {audioDiagnostics.ClippedSampleCount:N0}; emergency mute {audioDiagnostics.EmergencyMute}.",
            $"Test bench: {(testBenchSnapshot.IsActive ? "active" : "inactive")}; signal {testBenchSnapshot.SelectedSignalName}; output {testBenchSnapshot.OutputDisplayName}; peak {testBenchSnapshot.OutputPeakLevel:0.000}.",
            $"Output: {outputStatus.DisplayName} ({outputStatus.State}); streaming {outputStatus.IsStreaming}; hardware required {outputStatus.RequiresPhysicalHardware}; manual debug {outputStatus.IsManualDebugOnly}; hardware-absent mode {audioDiagnostics.HardwareAbsentMode}; null buffers {pipelineSnapshot.NullOutput?.SubmittedBufferCount ?? 0:N0}; render callbacks {outputStatus.RenderCallbackCount:N0}; backend callbacks {outputStatus.BackendCallbackCount:N0}; output buffers {outputStatus.SubmittedBufferCount:N0}; drops {outputStatus.DroppedBufferCount:N0}; underruns {outputStatus.UnderrunCount:N0}; render {FormatDuration(outputStatus.LastRenderDuration)}; jitter {FormatDuration(outputStatus.LastCallbackJitter)}.",
            $"Input discovery: {BuildInputDiscoveryDiagnosticsText()}",
            $"Paddle input listener: {BuildPaddleInputDiagnosticsText()}",
            $"ASIO readiness: {_asioReadinessSnapshot.Message} Drivers reported {_asioReadinessSnapshot.DriverNames.Count}; M-Audio match {(_asioReadinessSnapshot.MTrackDriverVisible ? "yes" : "no")}; channel {(_asioReadinessSnapshot.SelectedOutputChannel is null ? "none" : _asioReadinessSnapshot.SelectedOutputChannel)}; armed {_asioReadinessSnapshot.IsArmed}; Windows sound output proves ASIO {_asioReadinessSnapshot.WindowsSoundOutputVisibilityProvesAsio}.",
            $"Runtime prerequisites: .NET {Environment.Version}; WPF desktop runtime is present because the app is running; launch script sets DOTNET_ROOT to the repo-local runtime before starting the executable.",
            $"App settings: {_settingsStore.SettingsPath}; {(_settingsError ?? "loaded")}; theme {(_lightTheme ? "light" : "dark")}; persisted ASIO driver {(_selectedAsioDriverName ?? "none")}; persisted ASIO channel {(_selectedAsioOutputChannel is null ? "none" : _selectedAsioOutputChannel)}; persisted paddle mapping device {_paddleMapping.SelectedDeviceId ?? "none"} left {FormatButtonMapping(_paddleMapping.LeftPaddleButtonId)} right {FormatButtonMapping(_paddleMapping.RightPaddleButtonId)}; ASIO armed state, haptics running state, emergency mute, and P-HPR control are not persisted."
        };

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Diagnostics" })
        {
            PageStatusText.Text = DiagnosticsSummaryText.Text;
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

    private string BuildInputDiscoveryDiagnosticsText()
    {
        var snapshot = _inputDiscoverySnapshot;
        if (!snapshot.HasRun)
        {
            return "not refreshed; manual Refresh Input Devices is available on Devices; no listener, mapping, routing, or output is active.";
        }

        var errors = snapshot.Errors.Count == 0 ? "none" : string.Join("; ", snapshot.Errors);
        return $"{snapshot.DeviceCount:N0} device(s); methods {FormatDiscoveryMethods(snapshot.Methods)}; wheelbase {snapshot.LikelySimagicWheelBaseCandidates.Count:N0}; GT Neo/wheel {snapshot.LikelyGtNeoWheelInputCandidates.Count:N0}; P700 {snapshot.LikelyP700PedalCandidates.Count:N0}; unknown HID/game-controller {snapshot.UnknownHidOrGameControllerCandidates.Count:N0}; errors {errors}; read-only discovery with Stage 2E Windows game-controller listener available separately.";
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

        return $"{snapshot.Status}; selected {selected}; method {_paddleMapping.SelectedMethod}; left {FormatButtonMapping(snapshot.Mapping.LeftPaddleButtonId)} state {snapshot.LeftPaddleState}; right {FormatButtonMapping(snapshot.Mapping.RightPaddleButtonId)} state {snapshot.RightPaddleState}; last raw {lastRaw}; last mapped {lastMapped}; count {snapshot.PaddlePressCount:N0}; debounce {snapshot.Mapping.DebounceDuration.TotalMilliseconds:0} ms; error {snapshot.LastErrorMessage ?? "none"}; diagnostics only, no haptic output.";
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

    private string BuildForwardingDestinationsText()
    {
        return _forwardingDestinations.Count == 0
            ? "none configured"
            : string.Join("; ", _forwardingDestinations.Select(destination =>
                $"{(destination.Enabled ? "enabled" : "disabled")} {destination.Name}->{destination.Host}:{destination.Port}"));
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
        UpdatePaddleInputStatus();
    }

    private void PaddleInputSource_InputChanged(object? sender, object e)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            UpdatePaddleInputStatus();
            UpdateDiagnosticsStatus();
        });
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
                : "Recording idle; latest or selected replay can feed the same haptic pipeline.";
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
        _paddleInputSource.DisposeAsync().AsTask().GetAwaiter().GetResult();
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

    private sealed record ForwardingDestinationListItem(
        int Index,
        ForwardingDestinationSetting Setting,
        string DisplayText);

    private sealed record PaddleDeviceListItem(
        InputDeviceSelection Selection,
        string DisplayText);

    private sealed record RecordingLibraryItem(
        string Path,
        string DisplayText,
        string DetailText);

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
