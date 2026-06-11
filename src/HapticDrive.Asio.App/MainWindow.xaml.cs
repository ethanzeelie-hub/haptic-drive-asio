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
using System.Globalization;
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
    private readonly DrivingArmedStateService _drivingArmedStateService = new();
    private readonly ShiftIntentProcessor _shiftIntentProcessor;
    private readonly MockPhprOutputDevice _mockPhprOutput = new();
    private readonly SafetyLimitedPhprOutputDevice _mockPhprSafetyOutput;
    private readonly WindowsHidReportWriter _realPhprHidWriter = new();
    private readonly SimagicPhprOutputDevice _realPhprOutput;
    private readonly IPHprDirectPulseService _phprDirectPulseService;
    private readonly IPHprDirectCommandDispatcher _phprDirectCommandDispatcher;
    private readonly IPHprDirectRuntime _phprDirectRuntime;
    private readonly PHprDirectGearPulseRouter _realPhprGearPulseRouter;
    private readonly PHprRoadVibrationRouter _realRoadVibrationRouter;
    private readonly PHprSlipLockRouter _realSlipLockRouter;
    private readonly PHprManualValidationResultExporter _phprValidationExporter = new();
    private readonly PHprHidOpenCheckRunner _phprHidOpenCheckRunner = new();
    private readonly PHprGearPulseRouter _mockGearPulseRouter;
    private readonly PHprPedalEffectsRouter _mockPedalEffectsRouter;
    private readonly PaddleGearBenchTestController _paddleGearBenchTestController = new(PaddleGearBenchTestOptions.EnabledDirect);
    private readonly IPHprSoftwareCoexistenceDetector _phprSoftwareCoexistenceDetector =
        new PHprSoftwareCoexistenceDetector(new WindowsProcessSnapshotProvider());
    private readonly IPHprDirectOutputCandidateProvider _phprDirectOutputCandidateProvider =
        new WindowsPhprDirectOutputCandidateProvider();
    private readonly IUdpTelemetryReceiver _telemetryReceiver = new UdpTelemetryReceiver();
    private readonly HapticProfileStore _profileStore = new();
    private readonly PhprEffectProfileStore _phprProfileStore = new();
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
    private readonly IReadOnlyList<ShiftIntentMode> _shiftIntentModeOptions = Enum.GetValues<ShiftIntentMode>();
    private readonly IReadOnlyList<PHprGearPulseTarget> _mockGearPulseTargetOptions = Enum.GetValues<PHprGearPulseTarget>();
    private readonly IReadOnlyList<PHprGearPulseTarget> _paddleGearBenchTargetOptions = Enum.GetValues<PHprGearPulseTarget>();
    private readonly IReadOnlyList<PaddleGearBenchTestOutputMode> _paddleGearBenchOutputModeOptions =
        Enum.GetValues<PaddleGearBenchTestOutputMode>();
    private readonly IReadOnlyList<int> _manualAsioHardwareTestDurationOptions = [250, 500, 1000];
    private readonly IReadOnlyList<PHprGearPulseTarget> _mockPedalEffectTargetOptions = Enum.GetValues<PHprGearPulseTarget>();
    private readonly IReadOnlyList<PHprHidReportTransport> _realPhprReportTransportOptions =
        Enum.GetValues<PHprHidReportTransport>();
    private readonly IReadOnlyList<PhprPedalsModeOption> _phprPedalsModeOptions =
    [
        new(PhprPedalsMode.Disabled, "Disabled"),
        new(PhprPedalsMode.Mock, "Mock"),
        new(PhprPedalsMode.Direct, "Direct")
    ];

    private readonly IReadOnlyList<ShellPageDefinition> _pages =
    [
        new(
            "Dashboard",
            "Dashboard",
            "Safe overview for telemetry, haptics, and hardware-absent operation.",
            "Hardware-absent safe startup active",
            [
                "NullAudioOutputDevice is the default safe output.",
                "F1 25 packets stay raw for recording and forwarding.",
                "P-HPR direct hardware output never starts on launch; startup only auto-selects and validates a known direct-ready device."
            ]),
        new(
            "Devices",
            "Devices",
            "Bass shaker output, Simagic P-HPR pedals, and wheel paddle input.",
            "Safe device controls available",
            [
                "ASIO absence does not block startup, builds, or tests.",
                "P-HPR mock mode never writes hardware reports.",
                "Read-only paddle input can drive InstantPaddleOnly shift intent through safety gates."
            ]),
        new(
            "Effects",
            "Effects",
            "Hardware-safe generated effect diagnostics.",
            "Effect tuning drives the output-owned render path",
            [
                "Engine, gear shift, kerb, impact, road texture, slip, and brake-lock effects consume VehicleState.",
                "P-HPR pedal effects remain separate from the ASIO/BST-1 audio path.",
                "Physical tuning waits for local hardware validation."
            ]),
        new(
            "Routing / Mixer",
            "Routing / Mixer",
            "Hardware-safe mixer level controls and safety-chain visibility.",
            "Mixer and safety controls available",
            [
                "ASIO/BST-1 routing remains audio-only.",
                "P-HPR modules use a separate actuator path.",
                "Emergency mute and limiter diagnostics stay visible."
            ]),
        new(
            "Telemetry / UDP",
            "Telemetry / UDP",
            "Raw F1 25 UDP input, byte-preserving forwarding, recording, and replay.",
            "UDP listener, forwarder, recorder, parser, and VehicleState adapter active",
            [
                "Default listen port is 20778.",
                "Forwarding sends exact packet bytes to enabled destinations.",
                "Recording captures raw incoming UDP payload bytes before parser validation.",
                "Replay emits recorded packets through the same parser and VehicleState path."
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
            "Advanced / Diagnostics",
            "Advanced / Diagnostics",
            "Diagnostics, synthetic test bench, app preferences, and guarded P-HPR research controls.",
            "Advanced diagnostics hidden until enabled",
            [
                "Advanced P-HPR direct-control, validation, and mock internals stay collapsed by default.",
                "The synthetic test bench remains Null-output friendly.",
                "Copyable diagnostics keep parser, mixer, output, input, and P-HPR safety state visible."
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
    private PHprSoftwareCoexistenceSnapshot _phprSoftwareCoexistenceSnapshot = PHprSoftwareCoexistenceSnapshot.NotScanned;
    private PHprControlledWriteReadiness _phprControlledWriteReadiness =
        PHprControlledWriteReadiness.Evaluate(PHprControlledWriteChecklist.Stage2PNoWriteDefault);
    private PHprManualValidationReadiness _phprManualValidationReadiness =
        PHprManualValidationReadiness.Evaluate(PHprManualValidationChecklist.Default);
    private PHprRealOutputOptions _realPhprOptions = PHprRealOutputOptions.Disabled;
    private PHprRoadVibrationRouterOptions _realRoadVibrationOptions = PHprRoadVibrationRouterOptions.Disabled;
    private PHprSlipLockRouterOptions _realSlipLockOptions = PHprSlipLockRouterOptions.Disabled;
    private WheelPaddleMapping _paddleMapping = WheelPaddleMapping.Default;
    private Task? _activeReplayTask;
    private bool _updatingTuningUi;
    private bool _updatingSettingsUi;
    private bool _updatingPaddleInputUi;
    private bool _updatingShiftIntentUi;
    private bool _updatingPaddleGearBenchUi;
    private bool _updatingAdvancedDiagnosticsUi;
    private bool _updatingPhprPedalsUi;
    private bool _updatingRealPhprDirectControlUi;
    private bool _updatingMockGearPulseUi;
    private bool _updatingMockPedalEffectsUi;
    private bool _advancedDiagnosticsEnabled;
    private bool _routingMockPedalEffects;
    private bool _routingRealRoadVibration;
    private bool _routingRealSlipLock;
    private DateTimeOffset? _lastPhprCoexistenceScanUtc;
    private string? _lastPhprValidationExportPath;
    private string _lastPhprPedalsPulseMessage = "No normal P-HPR test pulse has been sent.";
    private PHprDirectGearPulseRoutingResult? _lastRealPhprGearPulseRoutingResult;
    private PHprRoadVibrationRoutingResult? _lastRealRoadVibrationRoutingResult;
    private PHprSlipLockRoutingResult? _lastRealSlipLockRoutingResult;
    private List<ForwardingDestinationSetting> _forwardingDestinations = [];
    private List<ForwardingDestinationListItem> _forwardingDestinationItems = [];
    private List<PaddleDeviceListItem> _paddleDeviceItems = [];
    private List<PhprDirectOutputCandidateListItem> _realPhprCandidateItems = [];
    private List<RecordingLibraryItem> _recordingLibraryItems = [];

    public MainWindow()
    {
        _asioVisibilityDiagnostics = new AsioDriverVisibilityDiagnostics(_asioDriverCatalog);
        _asioReadinessDiagnostics = new AsioReadinessDiagnostics(_asioDriverCatalog);

        var appSettings = _settingsStore.Load();
        _settingsError = appSettings.LastStatusMessage;
        _lightTheme = appSettings.UseLightTheme;
        _advancedDiagnosticsEnabled = appSettings.AdvancedDiagnosticsEnabled;
        _selectedAsioDriverName = appSettings.LastAsioDriverName;
        _selectedAsioOutputChannel = appSettings.LastAsioOutputChannel;
        _forwardingDestinations = appSettings.ForwardingDestinations.ToList();
        _paddleMapping = CreatePaddleMapping(appSettings.PaddleInputMapping);
        _realPhprOptions = CreateRealPhprOutputOptions(appSettings.RealPhprGearPulseRouting);
        _realRoadVibrationOptions = CreateRealRoadVibrationRouterOptions(appSettings.RealPhprRoadVibrationRouting);
        _realSlipLockOptions = CreateRealSlipLockRouterOptions(appSettings.RealPhprSlipLockRouting);
        _shiftIntentProcessor = new ShiftIntentProcessor(
            _drivingArmedStateService,
            CreateShiftIntentOptions(appSettings.ShiftIntent));
        _mockPhprSafetyOutput = new SafetyLimitedPhprOutputDevice(_mockPhprOutput);
        _realPhprOutput = new SimagicPhprOutputDevice(_realPhprHidWriter, _realPhprOptions);
        _phprDirectPulseService = new PhprDeviceCardPulseService(_realPhprOutput);
        _phprDirectCommandDispatcher = new PHprDirectCommandDispatcher(_phprDirectPulseService, _realPhprOutput);
        var validationDirectory = GetLocalValidationResultsDirectory();
        _phprDirectRuntime = new PHprDirectRuntimeCoordinator(
            _realPhprOutput,
            _phprDirectPulseService,
            _phprDirectCommandDispatcher,
            new FilePHprBenchFlightRecorder(validationDirectory),
            new FilePHprBenchUncleanShutdownStore(validationDirectory),
            commitSummary: TryReadGitHeadSummary());
        _realPhprGearPulseRouter = new PHprDirectGearPulseRouter(_realPhprOutput, _realPhprOptions);
        _realRoadVibrationRouter = new PHprRoadVibrationRouter(
            _realPhprOutput,
            _realRoadVibrationOptions,
            _realPhprOutput.SetSafetyContext);
        _realSlipLockRouter = new PHprSlipLockRouter(
            _realPhprOutput,
            _realSlipLockOptions,
            _realPhprOutput.SetSafetyContext);
        _mockGearPulseRouter = new PHprGearPulseRouter(
            _mockPhprSafetyOutput,
            CreateMockGearPulseRouterOptions(appSettings.MockGearPulseRouting));
        _mockPedalEffectsRouter = new PHprPedalEffectsRouter(
            _mockPhprSafetyOutput,
            CreateMockPedalEffectsRouterOptions(appSettings.MockPedalEffectsRouting));
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
        ShiftIntentModeComboBox.ItemsSource = _shiftIntentModeOptions;
        PhprPedalsModeComboBox.ItemsSource = _phprPedalsModeOptions;
        MockGearPulseTargetComboBox.ItemsSource = _mockGearPulseTargetOptions;
        PaddleGearBenchTargetComboBox.ItemsSource = _paddleGearBenchTargetOptions;
        PaddleGearBenchOutputModeComboBox.ItemsSource = _paddleGearBenchOutputModeOptions;
        ManualAsioHardwareDurationComboBox.ItemsSource = _manualAsioHardwareTestDurationOptions;
        ManualAsioHardwareDurationComboBox.SelectedItem = 250;
        RoadPedalEffectTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        SlipPedalEffectTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        LockPedalEffectTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        RealSlipTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        RealLockTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        RealPhprReportTransportComboBox.ItemsSource = _realPhprReportTransportOptions;
        RealPhprReportTransportComboBox.SelectedItem = PHprHidReportTransport.OutputReport;
        ApplyTheme(_lightTheme);
        RefreshForwardingDestinationItems();
        ApplyProfileToControls(_currentProfile);
        ApplyProfileToRuntime(_currentProfile);
        UpdateProfileStatus("Default conservative profile loaded.", []);
        ApplyPaddleMappingToControls();
        ApplyShiftIntentSettingsToControls();
        ApplyMockGearPulseSettingsToControls();
        ApplyMockPedalEffectsSettingsToControls();
        ApplyPhprPedalsNormalSettingsToControls();
        ApplyPaddleGearBenchSettingsToControls();
        ApplyRealPhprOptionsToControls();
        ApplyAdvancedDiagnosticsPreferenceToControls();
        _paddleInputSource.RawButtonChanged += PaddleInputSource_InputChanged;
        _paddleInputSource.PaddleInputReceived += PaddleInputSource_InputChanged;
        _paddleInputSource.PaddleInputReceived += PaddleInputSource_PaddleInputReceived;
        _telemetryReceiver.PacketReceived += TelemetryReceiver_PacketReceived;
        _telemetryStatusTimer.Tick += TelemetryStatusTimer_Tick;
        Dispatcher.UnhandledException += MainWindow_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsioVisibilityDiagnosticsAsync();
        RefreshPhprSoftwareCoexistenceStatus(force: true);
        await RefreshInputDeviceDiscoveryAsync(isStartupRefresh: true);
        await RefreshRealPhprCandidateItemsAsync(autoSelectPreferred: true);
        ConfigurePhprDirectRuntime();
        await _phprDirectRuntime.InitializeStartupCleanupAsync();
        ApplyPaddleGearBenchRuntimeBlockToControls();

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
        UpdateManualAsioHardwareTestStatus();
        UpdateDiagnosticsStatus();
        UpdateForwardingEditorStatus();
        await RefreshRecordingLibraryAsync();
    }

    private void MainWindow_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WritePaddleGearBenchCrashLog("dispatcher-unhandled", e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WritePaddleGearBenchCrashLog("appdomain-unhandled", e.ExceptionObject as Exception);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WritePaddleGearBenchCrashLog("task-unobserved", e.Exception);
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
            MixerPanel.Visibility = page.NavigationLabel == "Routing / Mixer"
                ? Visibility.Visible
                : Visibility.Collapsed;
            DevicesPanel.Visibility = page.NavigationLabel == "Devices"
                ? Visibility.Visible
                : Visibility.Collapsed;
            var isTelemetryPage = page.NavigationLabel == "Telemetry / UDP";
            ForwardingPanel.Visibility = isTelemetryPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            RecordingsPanel.Visibility = isTelemetryPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            var isAdvancedPage = page.NavigationLabel == "Advanced / Diagnostics";
            AdvancedPhprDiagnosticsPanel.Visibility = isAdvancedPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            TestBenchPanel.Visibility = isAdvancedPage && _advancedDiagnosticsEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            ProfilesPanel.Visibility = page.NavigationLabel == "Profiles"
                ? Visibility.Visible
                : Visibility.Collapsed;
            SettingsPanel.Visibility = isAdvancedPage && _advancedDiagnosticsEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = isAdvancedPage && _advancedDiagnosticsEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            FooterStatusText.Text = $"Viewing {page.NavigationLabel} - P-HPR controls simplified";
            UpdateTelemetryStatus();
            UpdateEffectStatus();
            UpdateMixerStatus();
            UpdateDeviceStatus();
            UpdatePhprPedalsStatus();
            UpdateProfileStatus();
            UpdateTestBenchStatus();
            UpdateManualAsioHardwareTestStatus();
            UpdateDiagnosticsStatus();
            UpdateAdvancedDiagnosticsVisibility();
            if (isTelemetryPage)
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

        if (!PaddleInputDeviceSelector.HasUsableButtons(deviceItem.Device))
        {
            var blocked = $"Selected Windows game-controller reports {PaddleInputDeviceSelector.GetUsableButtonCount(deviceItem.Device):N0} usable buttons. Select the 32-button VID_3670/PID_0905 wheel input device before starting the listener.";
            PaddleInputStatusText.Text = blocked;
            FooterStatusText.Text = $"Paddle input listener was not started; {blocked}";
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
            diagnostics.Output.IsEmergencyStopActive);
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
            _realPhprOptions.CandidateIsRawInputOnly);
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
        var mode = ShiftIntentModeComboBox.SelectedItem is ShiftIntentMode selectedMode
            ? selectedMode
            : ShiftIntentMode.InstantPaddleOnly;
        _shiftIntentProcessor.Configure(new ShiftIntentProcessorOptions
        {
            IsEnabled = ShiftIntentEnabledCheckBox.IsChecked == true,
            Mode = mode
        });
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
        RefreshDrivingArmedAndShiftIntentTelemetry();
        UpdateHapticsStateText();
        UpdateRecordingStatus();
        UpdateOutputStatus(_hapticPipeline.GetSnapshot().Output);
        UpdateManualAsioHardwareTestStatus();
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
                autoSelection = PhprDirectAutoReadySelector.Select(
                    candidates,
                    _realPhprOptions,
                    enableWhenPreferredPresent: true);
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
                ApplyPhprPedalsNormalSettingsToControls();
                ApplyPaddleGearBenchSettingsToControls();
            }

            UpdateRealPhprDirectControlStatus();
            UpdatePhprPedalsStatus();
            UpdatePaddleGearBenchStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = autoSelection?.HasPreferredCandidate == true
                ? $"Preferred real P-HPR direct candidate selected; {_realPhprCandidateItems.Count:N0} candidate(s) found. Open-check used no output report or feature report."
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

    private async Task RunAutomaticRealPhprReadinessChecksAsync(PhprDirectAutoReadySelection selection)
    {
        var result = await _phprHidOpenCheckRunner.RunAsync(
            selection.Selector,
            selection.Options.CandidateHasOpenableHidPath,
            selection.Options.CandidateIsRawInputOnly);
        ApplyRealPhprOpenCheckResult(result);

        var diagnostics = _realPhprOutput.GetDiagnostics();
        var dryRun = PHprDirectOutputDryRunValidator.Validate(
            _realPhprOptions,
            _phprSoftwareCoexistenceSnapshot.Status,
            diagnostics.Output.IsEmergencyStopActive);
        RealPhprCandidatePickerStatusText.Text =
            $"{selection.Message} {result.Message} Dry-run can pulse {dryRun.CanPulse}; blockers {(dryRun.Issues.Count == 0 ? "none" : string.Join("; ", dryRun.Issues))}. No output report or feature report was sent.";
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
        var selector = item.Candidate.ToSelector(ParseOptionalReportIdOrNull(RealPhprReportIdTextBox.Text));
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

    private bool TryBuildMockGearPulseOptionsFromControls(
        out PHprGearPulseRouterOptions options,
        out string message)
    {
        var current = _mockGearPulseRouter.GetSnapshot().Options;
        options = current;

        if (!PhprUiValueConverter.TryParseStrengthPercent(
                MockGearPulseStrengthTextBox.Text,
                "Mock P-HPR",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                MockGearPulseFrequencyTextBox.Text,
                "Mock P-HPR",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                MockGearPulseDurationTextBox.Text,
                "Mock P-HPR",
                out var duration,
                out message))
        {
            return false;
        }

        var target = MockGearPulseTargetComboBox.SelectedItem is PHprGearPulseTarget selectedTarget
            ? selectedTarget
            : PHprGearPulseTarget.Both;
        options = new PHprGearPulseRouterOptions
        {
            IsEnabled = MockGearPulseEnabledCheckBox.IsChecked == true,
            TargetModule = target,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = strength,
                FrequencyHz = frequency,
                DurationMs = duration
            }
        }.Normalize();
        message = "Mock P-HPR gear pulse routing ready.";
        return true;
    }

    private bool TryBuildPaddleGearBenchOptionsFromControls(
        out PaddleGearBenchTestOptions options,
        out string message)
    {
        var current = _paddleGearBenchTestController.GetSnapshot().Options;
        options = current;

        var target = PaddleGearBenchTargetComboBox.SelectedItem is PHprGearPulseTarget selectedTarget
            ? selectedTarget
            : PHprGearPulseTarget.Both;
        var sourceSettings = target == PHprGearPulseTarget.Throttle
            ? _realPhprOptions.ThrottleGearPulse
            : _realPhprOptions.BrakeGearPulse;
        options = new PaddleGearBenchTestOptions
        {
            IsEnabled = PaddleGearBenchEnabledCheckBox.IsChecked == true,
            IsArmed = PaddleGearBenchEnabledCheckBox.IsChecked == true,
            OutputMode = PaddleGearBenchTestOutputMode.Direct,
            TargetModule = target,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = sourceSettings.Strength01,
                FrequencyHz = sourceSettings.FrequencyHz,
                DurationMs = sourceSettings.DurationMs
            }
        }.Normalize();
        message = "Paddle Gear Bench Test options ready.";
        return true;
    }

    private bool TryBuildMockPedalEffectsOptionsFromControls(
        out PHprPedalEffectsRouterOptions options,
        out string message)
    {
        var current = _mockPedalEffectsRouter.GetSnapshot().Options;
        options = current;

        if (!TryBuildPedalEffectState(
                PHprPedalEffectKind.RoadVibration,
                RoadPedalEffectEnabledCheckBox,
                RoadPedalEffectTargetComboBox,
                RoadPedalEffectStrengthTextBox,
                RoadPedalEffectFrequencyTextBox,
                RoadPedalEffectDurationTextBox,
                out var road,
                out message)
            || !TryBuildPedalEffectState(
                PHprPedalEffectKind.WheelSlip,
                SlipPedalEffectEnabledCheckBox,
                SlipPedalEffectTargetComboBox,
                SlipPedalEffectStrengthTextBox,
                SlipPedalEffectFrequencyTextBox,
                SlipPedalEffectDurationTextBox,
                out var slip,
                out message)
            || !TryBuildPedalEffectState(
                PHprPedalEffectKind.WheelLock,
                LockPedalEffectEnabledCheckBox,
                LockPedalEffectTargetComboBox,
                LockPedalEffectStrengthTextBox,
                LockPedalEffectFrequencyTextBox,
                LockPedalEffectDurationTextBox,
                out var wheelLock,
                out message))
        {
            return false;
        }

        options = current with
        {
            IsEnabled = MockPedalEffectsEnabledCheckBox.IsChecked == true,
            RoadVibration = road,
            WheelSlip = slip,
            WheelLock = wheelLock
        };
        options = options.Normalize();
        message = "Mock P-HPR pedal effects routing ready.";
        return true;
    }

    private bool TryBuildRealPhprOptionsFromControls(
        out PHprRealOutputOptions options,
        out string message)
    {
        var current = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        options = current;

        if (!TryParseOptionalReportId(RealPhprReportIdTextBox.Text, out var reportId, out message))
        {
            return false;
        }

        if (!int.TryParse(RealPhprReportLengthTextBox.Text.Trim(), out var reportLength)
            || reportLength is < 1 or > 1_024)
        {
            message = "Real P-HPR report length must be a whole number from 1 to 1024 bytes.";
            return false;
        }

        var transport = RealPhprReportTransportComboBox.SelectedItem is PHprHidReportTransport selectedTransport
            ? selectedTransport
            : PHprHidReportTransport.OutputReport;

        if (!TryBuildNormalPhprGearPulseSettings(
                "Brake",
                NormalPhprBrakeEnabledCheckBox,
                NormalPhprBrakeStrengthTextBox,
                NormalPhprBrakeFrequencyTextBox,
                NormalPhprBrakeDurationTextBox,
                out var brake,
                out message)
            || !TryBuildNormalPhprGearPulseSettings(
                "Throttle",
                NormalPhprThrottleEnabledCheckBox,
                NormalPhprThrottleStrengthTextBox,
                NormalPhprThrottleFrequencyTextBox,
                NormalPhprThrottleDurationTextBox,
                out var throttle,
                out message))
        {
            return false;
        }

        var directEnabled = RealPhprDirectControlEnabledCheckBox.IsChecked == true;
        var directArmed = directEnabled;
        var selectedCandidate = RealPhprCandidateComboBox.SelectedItem as PhprDirectOutputCandidateListItem;
        var selector = selectedCandidate?.Candidate.ToSelector(reportId, transport) ?? PHprHidDeviceSelector.None;
        selector = selector with
        {
            ReportId = reportId ?? selector.ReportId,
            ReportLength = reportLength,
            Transport = transport,
            InterfaceName = string.IsNullOrWhiteSpace(RealPhprInterfaceTextBox.Text)
                ? selector.InterfaceName
                : RealPhprInterfaceTextBox.Text.Trim()
        };
        var normalizedSelector = selector.Normalize();
        var previousSelector = current.Selector.Normalize();
        var candidateSourceMethod = selectedCandidate?.Candidate.SourceMethod ?? PHprDirectOutputCandidateSourceMethod.Unknown;
        var candidateIsRawInputOnly = selectedCandidate?.Candidate.IsRawInputOnly ?? false;
        var candidateHasOpenableHidPath = selectedCandidate?.Candidate.HasOpenableHidPath ?? false;
        var candidateOutputReportCapabilityKnown = selectedCandidate?.Candidate.HasKnownOutputReportCapability ?? false;
        var candidateFeatureReportCapabilityKnown = selectedCandidate?.Candidate.HasKnownFeatureReportCapability ?? false;
        var reportShape = PHprHidReportShapeValidator.Validate(selectedCandidate?.Candidate, normalizedSelector);
        var openCheckStillSameSelector = current.OpenCheckAttempted
            && SelectorMatchesForOpenCheck(previousSelector, normalizedSelector)
            && current.CandidateSourceMethod == candidateSourceMethod
            && current.CandidateIsRawInputOnly == candidateIsRawInputOnly
            && current.CandidateHasOpenableHidPath == candidateHasOpenableHidPath
            && current.CandidateOutputReportCapabilityKnown == candidateOutputReportCapabilityKnown
            && current.CandidateFeatureReportCapabilityKnown == candidateFeatureReportCapabilityKnown;
        options = current with
        {
            DirectControlEnabled = directEnabled,
            DirectControlArmed = directArmed,
            DirectControlApprovalConfirmed = directEnabled,
            CandidateSourceMethod = candidateSourceMethod,
            CandidateIsRawInputOnly = candidateIsRawInputOnly,
            CandidateHasOpenableHidPath = candidateHasOpenableHidPath,
            CandidateOutputReportCapabilityKnown = candidateOutputReportCapabilityKnown,
            CandidateFeatureReportCapabilityKnown = candidateFeatureReportCapabilityKnown,
            ReportShapeValidationAttempted = reportShape.Attempted,
            ReportShapeValidationSucceeded = reportShape.Succeeded,
            ReportShapeValidationFailed = reportShape.Failed,
            ReportShapeValidationMessage = reportShape.Message,
            OpenCheckAttempted = openCheckStillSameSelector && current.OpenCheckAttempted,
            OpenCheckSucceeded = openCheckStillSameSelector && current.OpenCheckSucceeded,
            OpenCheckFailed = openCheckStillSameSelector && current.OpenCheckFailed,
            OpenCheckSanitizedErrorCategory = openCheckStillSameSelector ? current.OpenCheckSanitizedErrorCategory : null,
            Selector = normalizedSelector,
            BrakeGearPulse = brake,
            ThrottleGearPulse = throttle
        };
        options = options.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = "Real P-HPR direct-control options ready for this session only.";
        return true;
    }

    private bool TryBuildRealRoadVibrationOptionsFromControls(
        out PHprRoadVibrationRouterOptions options,
        out string message)
    {
        var current = _realRoadVibrationOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        options = current;

        if (!TryBuildRealRoadVibrationPedalSettings(
                "Brake road",
                RealRoadBrakeEnabledCheckBox,
                RealRoadBrakeMinStrengthTextBox,
                RealRoadBrakeStrengthTextBox,
                RealRoadBrakeMinFrequencyTextBox,
                RealRoadBrakeFrequencyTextBox,
                RealRoadBrakeDurationTextBox,
                out var brake,
                out message)
            || !TryBuildRealRoadVibrationPedalSettings(
                "Throttle road",
                RealRoadThrottleEnabledCheckBox,
                RealRoadThrottleMinStrengthTextBox,
                RealRoadThrottleStrengthTextBox,
                RealRoadThrottleMinFrequencyTextBox,
                RealRoadThrottleFrequencyTextBox,
                RealRoadThrottleDurationTextBox,
                out var throttle,
                out message))
        {
            return false;
        }

        options = current with
        {
            IsEnabled = RealRoadVibrationEnabledCheckBox.IsChecked == true,
            Brake = brake,
            Throttle = throttle
        };
        options = options.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = "Real P-HPR road-vibration options ready.";
        return true;
    }

    private bool TryBuildRealSlipLockOptionsFromControls(
        out PHprSlipLockRouterOptions options,
        out string message)
    {
        var current = _realSlipLockOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        options = current;

        if (!TryBuildRealSlipLockEffectSettings(
                "Wheel slip",
                PHprPedalEffectKind.WheelSlip,
                RealSlipEnabledCheckBox,
                RealSlipTargetComboBox,
                RealSlipMinStrengthTextBox,
                RealSlipStrengthTextBox,
                RealSlipMinFrequencyTextBox,
                RealSlipFrequencyTextBox,
                RealSlipDurationTextBox,
                out var slip,
                out message)
            || !TryBuildRealSlipLockEffectSettings(
                "Wheel lock",
                PHprPedalEffectKind.WheelLock,
                RealLockEnabledCheckBox,
                RealLockTargetComboBox,
                RealLockMinStrengthTextBox,
                RealLockStrengthTextBox,
                RealLockMinFrequencyTextBox,
                RealLockFrequencyTextBox,
                RealLockDurationTextBox,
                out var wheelLock,
                out message))
        {
            return false;
        }

        options = current with
        {
            IsEnabled = RealSlipLockEnabledCheckBox.IsChecked == true,
            WheelSlip = slip,
            WheelLock = wheelLock
        };
        options = options.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = "Real P-HPR slip/lock options ready.";
        return true;
    }

    private static bool TryBuildRealGearPulseSettings(
        string label,
        CheckBox enabledCheckBox,
        TextBox strengthTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox,
        out PHprRealGearPulseSettings settings,
        out string message)
    {
        settings = PHprRealGearPulseSettings.Default;

        if (!PhprUiValueConverter.TryParseStrengthPercent(
                strengthTextBox.Text,
                $"{label} real P-HPR",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                frequencyTextBox.Text,
                $"{label} real P-HPR",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                durationTextBox.Text,
                $"{label} real P-HPR",
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprRealGearPulseSettings
        {
            IsEnabled = enabledCheckBox.IsChecked == true,
            Strength01 = strength,
            FrequencyHz = frequency,
            DurationMs = duration
        }.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = $"{label} real P-HPR pulse settings ready.";
        return true;
    }

    private static bool TryBuildRealRoadVibrationPedalSettings(
        string label,
        CheckBox enabledCheckBox,
        TextBox minimumStrengthTextBox,
        TextBox strengthTextBox,
        TextBox minimumFrequencyTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox,
        out PHprRoadVibrationPedalSettings settings,
        out string message)
    {
        settings = PHprRoadVibrationPedalSettings.Default;

        if (!PhprUiValueConverter.TryParseStrengthPercent(
                minimumStrengthTextBox.Text,
                $"{label} minimum",
                out var minimumStrength,
                out message)
            || !PhprUiValueConverter.TryParseStrengthPercent(
                strengthTextBox.Text,
                $"{label} maximum",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                minimumFrequencyTextBox.Text,
                $"{label} minimum",
                out var minimumFrequency,
                out message)
            || !PhprUiValueConverter.TryParseFrequencyHz(
                frequencyTextBox.Text,
                $"{label} maximum",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                durationTextBox.Text,
                label,
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprRoadVibrationPedalSettings
        {
            IsEnabled = enabledCheckBox.IsChecked == true,
            MinimumStrength01 = minimumStrength,
            Strength01 = strength,
            MinimumFrequencyHz = minimumFrequency,
            FrequencyHz = frequency,
            DurationMs = duration
        }.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = $"{label} real P-HPR road settings ready.";
        return true;
    }

    private static bool TryBuildRealSlipLockEffectSettings(
        string label,
        PHprPedalEffectKind kind,
        CheckBox enabledCheckBox,
        ComboBox targetComboBox,
        TextBox minimumStrengthTextBox,
        TextBox strengthTextBox,
        TextBox minimumFrequencyTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox,
        out PHprSlipLockEffectSettings settings,
        out string message)
    {
        settings = PHprSlipLockEffectSettings.DefaultFor(kind);

        var target = targetComboBox.SelectedItem is PHprGearPulseTarget selectedTarget
            ? selectedTarget
            : settings.TargetModule;

        if (!PhprUiValueConverter.TryParseStrengthPercent(
                minimumStrengthTextBox.Text,
                $"{label} minimum",
                out var minimumStrength,
                out message)
            || !PhprUiValueConverter.TryParseStrengthPercent(
                strengthTextBox.Text,
                $"{label} maximum",
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                minimumFrequencyTextBox.Text,
                $"{label} minimum",
                out var minimumFrequency,
                out message)
            || !PhprUiValueConverter.TryParseFrequencyHz(
                frequencyTextBox.Text,
                $"{label} maximum",
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                durationTextBox.Text,
                label,
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprSlipLockEffectSettings
        {
            IsEnabled = enabledCheckBox.IsChecked == true,
            TargetModule = target,
            MinimumStrength01 = minimumStrength,
            Strength01 = strength,
            MinimumFrequencyHz = minimumFrequency,
            FrequencyHz = frequency,
            DurationMs = duration
        }.Normalize(kind, SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = $"{label} real P-HPR slip/lock settings ready.";
        return true;
    }

    private static bool TryParseOptionalReportId(string text, out byte? reportId, out string message)
    {
        reportId = null;
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            message = "No report ID selected.";
            return true;
        }

        var style = NumberStyles.Integer;
        var valueText = trimmed;
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            style = NumberStyles.HexNumber;
            valueText = trimmed[2..];
        }
        else if (trimmed.Any(char.IsAsciiHexDigit) && trimmed.Any(char.IsAsciiLetter))
        {
            style = NumberStyles.HexNumber;
        }

        if (!byte.TryParse(valueText, style, CultureInfo.InvariantCulture, out var parsed))
        {
            message = "Real P-HPR report ID must be blank, 0-255, 0xF1, or F1.";
            return false;
        }

        reportId = parsed;
        message = "Report ID ready.";
        return true;
    }

    private static byte? ParseOptionalReportIdOrNull(string text)
    {
        return TryParseOptionalReportId(text, out var reportId, out _)
            ? reportId
            : null;
    }

    private static string FormatReportId(byte? reportId)
    {
        return reportId is null ? "none" : $"0x{reportId.Value:X2} ({reportId.Value.ToString(CultureInfo.InvariantCulture)})";
    }

    private static bool SelectorMatchesForOpenCheck(
        PHprHidDeviceSelector previous,
        PHprHidDeviceSelector current)
    {
        return string.Equals(previous.DevicePath, current.DevicePath, StringComparison.Ordinal)
            && previous.ReportId == current.ReportId
            && previous.ReportLength == current.ReportLength
            && previous.Transport == current.Transport;
    }

    private static bool TryBuildPedalEffectState(
        PHprPedalEffectKind kind,
        CheckBox enabledCheckBox,
        ComboBox targetComboBox,
        TextBox strengthTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox,
        out PHprPedalEffectState state,
        out string message)
    {
        var defaults = PHprPedalEffectState.DefaultFor(kind);
        state = defaults;
        var label = FormatPedalEffectKind(kind);

        if (!PhprUiValueConverter.TryParseStrengthPercent(
                strengthTextBox.Text,
                label,
                out var strength,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseFrequencyHz(
                frequencyTextBox.Text,
                label,
                out var frequency,
                out message))
        {
            return false;
        }

        if (!PhprUiValueConverter.TryParseDurationMs(
                durationTextBox.Text,
                label,
                out var duration,
                out message))
        {
            return false;
        }

        var target = targetComboBox.SelectedItem is PHprGearPulseTarget selectedTarget
            ? selectedTarget
            : defaults.TargetModule;
        state = defaults with
        {
            IsEnabled = enabledCheckBox.IsChecked == true,
            TargetModule = target,
            Profile = defaults.Profile with
            {
                Strength01 = strength,
                FrequencyHz = frequency,
                DurationMs = duration
            }
        };
        state = state.Normalize(kind);
        message = $"{label} pedal effect ready.";
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

    private static ShiftIntentProcessorOptions CreateShiftIntentOptions(ShiftIntentSetting setting)
    {
        return new ShiftIntentProcessorOptions
        {
            IsEnabled = setting.IsEnabled,
            Mode = setting.Mode
        }.Normalize();
    }

    private static PHprGearPulseRouterOptions CreateMockGearPulseRouterOptions(MockGearPulseRoutingSetting setting)
    {
        return new PHprGearPulseRouterOptions
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            Profile = PHprGearPulseProfile.Default with
            {
                Strength01 = setting.Strength01,
                FrequencyHz = setting.FrequencyHz,
                DurationMs = setting.DurationMs
            }
        }.Normalize();
    }

    private static PHprPedalEffectsRouterOptions CreateMockPedalEffectsRouterOptions(MockPedalEffectsRoutingSetting setting)
    {
        return new PHprPedalEffectsRouterOptions
        {
            IsEnabled = setting.IsEnabled,
            RoadVibration = CreatePedalEffectState(PHprPedalEffectKind.RoadVibration, setting.RoadVibration),
            WheelSlip = CreatePedalEffectState(PHprPedalEffectKind.WheelSlip, setting.WheelSlip),
            WheelLock = CreatePedalEffectState(PHprPedalEffectKind.WheelLock, setting.WheelLock)
        }.Normalize();
    }

    private static PHprRealOutputOptions CreateRealPhprOutputOptions(RealPhprGearPulseRoutingSetting setting)
    {
        return PHprRealOutputOptions.Disabled with
        {
            BrakeGearPulse = CreateRealGearPulseSettings(setting.Brake),
            ThrottleGearPulse = CreateRealGearPulseSettings(setting.Throttle)
        };
    }

    private static PHprRealGearPulseSettings CreateRealGearPulseSettings(RealPhprGearPulseSetting setting)
    {
        return new PHprRealGearPulseSettings
        {
            IsEnabled = setting.IsEnabled,
            Strength01 = setting.Strength01,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
    }

    private static PHprRoadVibrationRouterOptions CreateRealRoadVibrationRouterOptions(RealPhprRoadVibrationRoutingSetting setting)
    {
        return PHprRoadVibrationRouterOptions.Disabled with
        {
            IsEnabled = setting.IsEnabled,
            Brake = CreateRealRoadVibrationPedalSettings(setting.Brake),
            Throttle = CreateRealRoadVibrationPedalSettings(setting.Throttle)
        };
    }

    private static PHprRoadVibrationPedalSettings CreateRealRoadVibrationPedalSettings(RealPhprRoadVibrationPedalSetting setting)
    {
        return new PHprRoadVibrationPedalSettings
        {
            IsEnabled = setting.IsEnabled,
            MinimumStrength01 = setting.MinimumStrength01,
            Strength01 = setting.Strength01,
            MinimumFrequencyHz = setting.MinimumFrequencyHz,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
    }

    private static PHprSlipLockRouterOptions CreateRealSlipLockRouterOptions(RealPhprSlipLockRoutingSetting setting)
    {
        return PHprSlipLockRouterOptions.Disabled with
        {
            IsEnabled = setting.IsEnabled,
            WheelSlip = CreateRealSlipLockEffectSettings(PHprPedalEffectKind.WheelSlip, setting.WheelSlip),
            WheelLock = CreateRealSlipLockEffectSettings(PHprPedalEffectKind.WheelLock, setting.WheelLock)
        };
    }

    private static PHprSlipLockEffectSettings CreateRealSlipLockEffectSettings(
        PHprPedalEffectKind kind,
        RealPhprSlipLockEffectSetting setting)
    {
        return new PHprSlipLockEffectSettings
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            MinimumStrength01 = setting.MinimumStrength01,
            Strength01 = setting.Strength01,
            MinimumFrequencyHz = setting.MinimumFrequencyHz,
            FrequencyHz = setting.FrequencyHz,
            DurationMs = setting.DurationMs
        }.Normalize(kind, SimagicPhprOutputDevice.DirectControlSafetyLimits);
    }

    private static PHprPedalEffectState CreatePedalEffectState(
        PHprPedalEffectKind kind,
        MockPedalEffectSetting setting)
    {
        var defaults = PHprPedalEffectState.DefaultFor(kind);
        return defaults with
        {
            IsEnabled = setting.IsEnabled,
            TargetModule = setting.TargetModule,
            Profile = defaults.Profile with
            {
                Strength01 = setting.Strength01,
                FrequencyHz = setting.FrequencyHz,
                DurationMs = setting.DurationMs
            }
        };
    }

    private ShiftIntentSetting CreateShiftIntentSetting()
    {
        var snapshot = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        return new ShiftIntentSetting
        {
            IsEnabled = snapshot.IsEnabled,
            Mode = snapshot.Mode
        };
    }

    private MockGearPulseRoutingSetting CreateMockGearPulseRoutingSetting()
    {
        var snapshot = _mockGearPulseRouter.GetSnapshot();
        return new MockGearPulseRoutingSetting
        {
            IsEnabled = snapshot.Options.IsEnabled,
            TargetModule = snapshot.Options.TargetModule,
            Strength01 = snapshot.Options.Profile.Strength01,
            FrequencyHz = snapshot.Options.Profile.FrequencyHz,
            DurationMs = snapshot.Options.Profile.DurationMs
        };
    }

    private MockPedalEffectsRoutingSetting CreateMockPedalEffectsRoutingSetting()
    {
        var snapshot = _mockPedalEffectsRouter.GetSnapshot();
        return new MockPedalEffectsRoutingSetting
        {
            IsEnabled = snapshot.Options.IsEnabled,
            RoadVibration = CreateMockPedalEffectSetting(snapshot.Options.RoadVibration),
            WheelSlip = CreateMockPedalEffectSetting(snapshot.Options.WheelSlip),
            WheelLock = CreateMockPedalEffectSetting(snapshot.Options.WheelLock)
        };
    }

    private RealPhprGearPulseRoutingSetting CreateRealPhprGearPulseRoutingSetting()
    {
        var options = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return new RealPhprGearPulseRoutingSetting
        {
            Brake = RealPhprGearPulseSetting.From(options.BrakeGearPulse),
            Throttle = RealPhprGearPulseSetting.From(options.ThrottleGearPulse)
        };
    }

    private RealPhprRoadVibrationRoutingSetting CreateRealPhprRoadVibrationRoutingSetting()
    {
        var options = _realRoadVibrationOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return new RealPhprRoadVibrationRoutingSetting
        {
            IsEnabled = options.IsEnabled,
            Brake = RealPhprRoadVibrationPedalSetting.From(options.Brake),
            Throttle = RealPhprRoadVibrationPedalSetting.From(options.Throttle)
        };
    }

    private RealPhprSlipLockRoutingSetting CreateRealPhprSlipLockRoutingSetting()
    {
        var options = _realSlipLockOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return new RealPhprSlipLockRoutingSetting
        {
            IsEnabled = options.IsEnabled,
            WheelSlip = RealPhprSlipLockEffectSetting.From(PHprPedalEffectKind.WheelSlip, options.WheelSlip),
            WheelLock = RealPhprSlipLockEffectSetting.From(PHprPedalEffectKind.WheelLock, options.WheelLock)
        };
    }

    private static MockPedalEffectSetting CreateMockPedalEffectSetting(PHprPedalEffectState state)
    {
        return new MockPedalEffectSetting
        {
            IsEnabled = state.IsEnabled,
            TargetModule = state.TargetModule,
            Strength01 = state.Profile.Strength01,
            FrequencyHz = state.Profile.FrequencyHz,
            DurationMs = state.Profile.DurationMs
        };
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
        _updatingPaddleGearBenchUi = true;
        PaddleGearBenchEnabledCheckBox.IsChecked = snapshot.Options.IsEnabled;
        PaddleGearBenchArmCheckBox.IsChecked = snapshot.Options.IsArmed;
        PaddleGearBenchTargetComboBox.SelectedItem = snapshot.Options.TargetModule;
        PaddleGearBenchOutputModeComboBox.SelectedItem = snapshot.Options.OutputMode;
        PaddleGearBenchStrengthTextBox.Text = PhprUiValueConverter.FormatPercent(snapshot.Options.Profile.Strength01);
        PaddleGearBenchFrequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(snapshot.Options.Profile.FrequencyHz);
        PaddleGearBenchDurationTextBox.Text = snapshot.Options.Profile.DurationMs.ToString(CultureInfo.InvariantCulture);
        _updatingPaddleGearBenchUi = false;
        UpdatePaddleGearBenchStatus();
    }

    private void ApplyMockGearPulseSettingsToControls()
    {
        var snapshot = _mockGearPulseRouter.GetSnapshot();
        _updatingMockGearPulseUi = true;
        MockGearPulseEnabledCheckBox.IsChecked = snapshot.Options.IsEnabled;
        MockGearPulseTargetComboBox.SelectedItem = snapshot.Options.TargetModule;
        MockGearPulseStrengthTextBox.Text = PhprUiValueConverter.FormatPercent(snapshot.Options.Profile.Strength01);
        MockGearPulseFrequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(snapshot.Options.Profile.FrequencyHz);
        MockGearPulseDurationTextBox.Text = snapshot.Options.Profile.DurationMs.ToString(CultureInfo.InvariantCulture);
        _updatingMockGearPulseUi = false;
        UpdateMockGearPulseStatus();
    }

    private void ApplyMockPedalEffectsSettingsToControls()
    {
        var snapshot = _mockPedalEffectsRouter.GetSnapshot();
        _updatingMockPedalEffectsUi = true;
        MockPedalEffectsEnabledCheckBox.IsChecked = snapshot.Options.IsEnabled;
        ApplyPedalEffectStateToControls(
            snapshot.Options.RoadVibration,
            RoadPedalEffectEnabledCheckBox,
            RoadPedalEffectTargetComboBox,
            RoadPedalEffectStrengthTextBox,
            RoadPedalEffectFrequencyTextBox,
            RoadPedalEffectDurationTextBox);
        ApplyPedalEffectStateToControls(
            snapshot.Options.WheelSlip,
            SlipPedalEffectEnabledCheckBox,
            SlipPedalEffectTargetComboBox,
            SlipPedalEffectStrengthTextBox,
            SlipPedalEffectFrequencyTextBox,
            SlipPedalEffectDurationTextBox);
        ApplyPedalEffectStateToControls(
            snapshot.Options.WheelLock,
            LockPedalEffectEnabledCheckBox,
            LockPedalEffectTargetComboBox,
            LockPedalEffectStrengthTextBox,
            LockPedalEffectFrequencyTextBox,
            LockPedalEffectDurationTextBox);
        _updatingMockPedalEffectsUi = false;
        UpdateMockPedalEffectsStatus();
    }

    private void ApplyRealPhprOptionsToControls()
    {
        var options = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        _updatingRealPhprDirectControlUi = true;
        RealPhprDirectControlEnabledCheckBox.IsChecked = options.DirectControlEnabled;
        RealPhprDirectControlArmCheckBox.IsChecked = options.DirectControlArmed;
        RealPhprCandidateComboBox.ItemsSource = _realPhprCandidateItems;
        RealPhprCandidateComboBox.DisplayMemberPath = nameof(PhprDirectOutputCandidateListItem.DisplayText);
        RealPhprCandidateComboBox.SelectedItem = _realPhprCandidateItems.FirstOrDefault(item =>
            options.Selector.IsSelected
            && string.Equals(item.Candidate.DevicePath, options.Selector.DevicePath, StringComparison.Ordinal));
        RealPhprInterfaceTextBox.Text = options.Selector.InterfaceName;
        RealPhprReportIdTextBox.Text = options.Selector.ReportId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        RealPhprReportLengthTextBox.Text = options.Selector.ReportLength.ToString(CultureInfo.InvariantCulture);
        RealPhprReportTransportComboBox.ItemsSource = _realPhprReportTransportOptions;
        RealPhprReportTransportComboBox.SelectedItem = options.Selector.Transport;
        RealPhprApprovalPhraseTextBox.Text = string.Empty;
        RealPhprCandidatePickerStatusText.Text = "Direct-output candidates have not been refreshed. Private HID paths are kept in memory only after refresh.";
        ApplyRealGearPulseSettingsToControls(
            options.BrakeGearPulse,
            RealPhprBrakeEnabledCheckBox,
            RealPhprBrakeStrengthTextBox,
            RealPhprBrakeFrequencyTextBox,
            RealPhprBrakeDurationTextBox);
        ApplyRealGearPulseSettingsToControls(
            options.ThrottleGearPulse,
            RealPhprThrottleEnabledCheckBox,
            RealPhprThrottleStrengthTextBox,
            RealPhprThrottleFrequencyTextBox,
            RealPhprThrottleDurationTextBox);
        var roadOptions = _realRoadVibrationOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        RealRoadVibrationEnabledCheckBox.IsChecked = roadOptions.IsEnabled;
        ApplyRealRoadVibrationSettingsToControls(
            roadOptions.Brake,
            RealRoadBrakeEnabledCheckBox,
            RealRoadBrakeMinStrengthTextBox,
            RealRoadBrakeStrengthTextBox,
            RealRoadBrakeMinFrequencyTextBox,
            RealRoadBrakeFrequencyTextBox,
            RealRoadBrakeDurationTextBox);
        ApplyRealRoadVibrationSettingsToControls(
            roadOptions.Throttle,
            RealRoadThrottleEnabledCheckBox,
            RealRoadThrottleMinStrengthTextBox,
            RealRoadThrottleStrengthTextBox,
            RealRoadThrottleMinFrequencyTextBox,
            RealRoadThrottleFrequencyTextBox,
            RealRoadThrottleDurationTextBox);
        var slipLockOptions = _realSlipLockOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        RealSlipLockEnabledCheckBox.IsChecked = slipLockOptions.IsEnabled;
        ApplyRealSlipLockSettingsToControls(
            PHprPedalEffectKind.WheelSlip,
            slipLockOptions.WheelSlip,
            RealSlipEnabledCheckBox,
            RealSlipTargetComboBox,
            RealSlipMinStrengthTextBox,
            RealSlipStrengthTextBox,
            RealSlipMinFrequencyTextBox,
            RealSlipFrequencyTextBox,
            RealSlipDurationTextBox);
        ApplyRealSlipLockSettingsToControls(
            PHprPedalEffectKind.WheelLock,
            slipLockOptions.WheelLock,
            RealLockEnabledCheckBox,
            RealLockTargetComboBox,
            RealLockMinStrengthTextBox,
            RealLockStrengthTextBox,
            RealLockMinFrequencyTextBox,
            RealLockFrequencyTextBox,
            RealLockDurationTextBox);
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
            : "Advanced diagnostics are hidden by default. Normal P-HPR mock controls, paddle mapping, and emergency stop are available on Devices without enabling this.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            TestBenchPanel.Visibility = _advancedDiagnosticsEnabled ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = _advancedDiagnosticsEnabled ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = _advancedDiagnosticsEnabled ? Visibility.Visible : Visibility.Collapsed;
            PageStatusText.Text = _advancedDiagnosticsEnabled
                ? "Advanced diagnostics visible; direct hardware output remains guarded."
                : "Advanced diagnostics hidden. Enable the checkbox to show research controls, test bench, settings, and full diagnostics.";
        }
    }

    private void ApplyPhprPedalsNormalSettingsToControls()
    {
        var mode = GetEffectivePhprPedalsMode();
        var options = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        _updatingPhprPedalsUi = true;
        PhprPedalsMasterEnableCheckBox.IsChecked = mode != PhprPedalsMode.Disabled;
        PhprPedalsModeComboBox.SelectedItem = _phprPedalsModeOptions.First(option => option.Mode == mode);
        ApplyRealGearPulseSettingsToControls(
            options.BrakeGearPulse,
            NormalPhprBrakeEnabledCheckBox,
            NormalPhprBrakeStrengthTextBox,
            NormalPhprBrakeFrequencyTextBox,
            NormalPhprBrakeDurationTextBox);
        ApplyRealGearPulseSettingsToControls(
            options.ThrottleGearPulse,
            NormalPhprThrottleEnabledCheckBox,
            NormalPhprThrottleStrengthTextBox,
            NormalPhprThrottleFrequencyTextBox,
            NormalPhprThrottleDurationTextBox);
        _updatingPhprPedalsUi = false;
        UpdatePhprPedalsStatus();
    }

    private bool ApplyPhprPedalsNormalOptionsFromControls(string footerMessage)
    {
        if (!TryBuildNormalPhprGearPulseSettings(
                "Brake",
                NormalPhprBrakeEnabledCheckBox,
                NormalPhprBrakeStrengthTextBox,
                NormalPhprBrakeFrequencyTextBox,
                NormalPhprBrakeDurationTextBox,
                out var brake,
                out var message)
            || !TryBuildNormalPhprGearPulseSettings(
                "Throttle",
                NormalPhprThrottleEnabledCheckBox,
                NormalPhprThrottleStrengthTextBox,
                NormalPhprThrottleFrequencyTextBox,
                NormalPhprThrottleDurationTextBox,
                out var throttle,
                out message))
        {
            PhprPedalsStatusText.Text = message;
            FooterStatusText.Text = message;
            UpdatePhprPedalsStatus();
            return false;
        }

        var mode = GetSelectedPhprPedalsMode();
        _realPhprOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits) with
        {
            DirectControlEnabled = mode == PhprPedalsMode.Direct,
            DirectControlArmed = mode == PhprPedalsMode.Direct,
            DirectControlApprovalConfirmed = mode == PhprPedalsMode.Direct,
            BrakeGearPulse = brake,
            ThrottleGearPulse = throttle
        };
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
        ApplyRealGearPulseSettingsToControls(
            _realPhprOptions.BrakeGearPulse,
            RealPhprBrakeEnabledCheckBox,
            RealPhprBrakeStrengthTextBox,
            RealPhprBrakeFrequencyTextBox,
            RealPhprBrakeDurationTextBox);
        ApplyRealGearPulseSettingsToControls(
            _realPhprOptions.ThrottleGearPulse,
            RealPhprThrottleEnabledCheckBox,
            RealPhprThrottleStrengthTextBox,
            RealPhprThrottleFrequencyTextBox,
            RealPhprThrottleDurationTextBox);
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

    private static bool TryBuildNormalPhprGearPulseSettings(
        string label,
        CheckBox enabledCheckBox,
        TextBox strengthTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox,
        out PHprRealGearPulseSettings settings,
        out string message)
    {
        settings = PHprRealGearPulseSettings.Default;
        if (!PhprUiValueConverter.TryParseStrengthPercent(
                strengthTextBox.Text,
                $"{label} P-HPR",
                out var strength,
                out message)
            || !PhprUiValueConverter.TryParseFrequencyHz(
                frequencyTextBox.Text,
                $"{label} P-HPR",
                out var frequency,
                out message)
            || !PhprUiValueConverter.TryParseDurationMs(
                durationTextBox.Text,
                $"{label} P-HPR",
                out var duration,
                out message))
        {
            return false;
        }

        settings = new PHprRealGearPulseSettings
        {
            IsEnabled = enabledCheckBox.IsChecked == true,
            Strength01 = strength,
            FrequencyHz = frequency,
            DurationMs = duration
        }.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        message = $"{label} P-HPR pulse ready.";
        return true;
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

    private PhprPedalsMode GetEffectivePhprPedalsMode()
    {
        if (_realPhprOptions.DirectControlEnabled)
        {
            return PhprPedalsMode.Direct;
        }

        var mockGear = _mockGearPulseRouter.GetSnapshot().Options.IsEnabled;
        var mockPedals = _mockPedalEffectsRouter.GetSnapshot().Options.IsEnabled;
        return mockGear || mockPedals
            ? PhprPedalsMode.Mock
            : PhprPedalsMode.Disabled;
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

        if (diagnostics.Output.IsEmergencyStopActive)
        {
            message = "emergency stop is active";
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
                : "Mock P-HPR pedals enabled. Test pulses generate mock frames only and do not require haptics to be running.",
            PhprPedalsMode.Direct => directReady
                ? "Direct P-HPR mode is ready for this session. Hardware writes remain manually gated."
                : $"Direct P-HPR mode is selected but not ready: {directMessage}.",
            _ => "P-HPR pedal mode unavailable."
        };
        PhprPedalsDeviceStatusText.Text =
            $"Mock output: {(mockSnapshot.IsEmergencyStopActive ? "emergency stop active" : "ready")}; accepted {mockSnapshot.AcceptedCommandCount:N0}, rejected {mockSnapshot.RejectedCommandCount:N0}. Direct output: {realDiagnostics.Connection.State}; selected {(realDiagnostics.Options.Selector.IsSelected ? "yes" : "no")}; transport {realDiagnostics.Options.Selector.Transport}; source {realDiagnostics.Options.CandidateSourceMethod}; raw-input-only {realDiagnostics.Options.CandidateIsRawInputOnly}; openable {realDiagnostics.Options.CandidateHasOpenableHidPath}; output report known {realDiagnostics.Options.CandidateOutputReportCapabilityKnown}; feature report known {realDiagnostics.Options.CandidateFeatureReportCapabilityKnown}; report-shape {realDiagnostics.Options.ReportShapeValidationAttempted}/{realDiagnostics.Options.ReportShapeValidationSucceeded}; open-check {realDiagnostics.Options.OpenCheckAttempted}/{realDiagnostics.Options.OpenCheckSucceeded}; enabled {realDiagnostics.Options.DirectControlEnabled}; emergency stop {realDiagnostics.Output.IsEmergencyStopActive}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}.";
        PhprPedalsLastResultText.Text = _lastPhprPedalsPulseMessage;
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

    private static void ApplyRealGearPulseSettingsToControls(
        PHprRealGearPulseSettings settings,
        CheckBox enabledCheckBox,
        TextBox strengthTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox)
    {
        var normalized = settings.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        enabledCheckBox.IsChecked = normalized.IsEnabled;
        strengthTextBox.Text = PhprUiValueConverter.FormatPercent(normalized.Strength01);
        frequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz);
        durationTextBox.Text = normalized.DurationMs.ToString(CultureInfo.InvariantCulture);
    }

    private static void ApplyRealRoadVibrationSettingsToControls(
        PHprRoadVibrationPedalSettings settings,
        CheckBox enabledCheckBox,
        TextBox minimumStrengthTextBox,
        TextBox strengthTextBox,
        TextBox minimumFrequencyTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox)
    {
        var normalized = settings.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        enabledCheckBox.IsChecked = normalized.IsEnabled;
        minimumStrengthTextBox.Text = PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01);
        strengthTextBox.Text = PhprUiValueConverter.FormatPercent(normalized.Strength01);
        minimumFrequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz);
        frequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz);
        durationTextBox.Text = normalized.DurationMs.ToString(CultureInfo.InvariantCulture);
    }

    private static void ApplyRealSlipLockSettingsToControls(
        PHprPedalEffectKind kind,
        PHprSlipLockEffectSettings settings,
        CheckBox enabledCheckBox,
        ComboBox targetComboBox,
        TextBox minimumStrengthTextBox,
        TextBox strengthTextBox,
        TextBox minimumFrequencyTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox)
    {
        var normalized = settings.Normalize(kind, SimagicPhprOutputDevice.DirectControlSafetyLimits);
        enabledCheckBox.IsChecked = normalized.IsEnabled;
        targetComboBox.SelectedItem = normalized.TargetModule;
        minimumStrengthTextBox.Text = PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01);
        strengthTextBox.Text = PhprUiValueConverter.FormatPercent(normalized.Strength01);
        minimumFrequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz);
        frequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz);
        durationTextBox.Text = normalized.DurationMs.ToString(CultureInfo.InvariantCulture);
    }

    private static void ApplyPedalEffectStateToControls(
        PHprPedalEffectState state,
        CheckBox enabledCheckBox,
        ComboBox targetComboBox,
        TextBox strengthTextBox,
        TextBox frequencyTextBox,
        TextBox durationTextBox)
    {
        enabledCheckBox.IsChecked = state.IsEnabled;
        targetComboBox.SelectedItem = state.TargetModule;
        strengthTextBox.Text = PhprUiValueConverter.FormatPercent(state.Profile.Strength01);
        frequencyTextBox.Text = PhprUiValueConverter.FormatFrequency(state.Profile.FrequencyHz);
        durationTextBox.Text = state.Profile.DurationMs.ToString(CultureInfo.InvariantCulture);
    }

    private void SaveAppSettings()
    {
        try
        {
            _settingsStore.Save(new AppSettings
            {
                UseLightTheme = _lightTheme,
                AdvancedDiagnosticsEnabled = _advancedDiagnosticsEnabled,
                LastAsioDriverName = _selectedAsioDriverName,
                LastAsioOutputChannel = _selectedAsioOutputChannel,
                ForwardingDestinations = _forwardingDestinations.ToList(),
                PaddleInputMapping = CreatePaddleMappingSetting(),
                ShiftIntent = CreateShiftIntentSetting(),
                MockGearPulseRouting = CreateMockGearPulseRoutingSetting(),
                MockPedalEffectsRouting = CreateMockPedalEffectsRoutingSetting(),
                RealPhprGearPulseRouting = CreateRealPhprGearPulseRoutingSetting(),
                RealPhprRoadVibrationRouting = CreateRealPhprRoadVibrationRoutingSetting(),
                RealPhprSlipLockRouting = CreateRealPhprSlipLockRoutingSetting()
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
        RefreshDrivingArmedAndShiftIntentTelemetry();
        UpdateOutputStatus(result.OutputResult?.Status ?? _hapticPipeline.GetSnapshot().Output);
        UpdateManualAsioHardwareTestStatus();
        UpdateEffectStatus();
        UpdateShiftIntentStatus();
        UpdateMockPedalEffectsStatus();
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
        UpdateManualAsioHardwareTestStatus();
    }

    private void UpdateHapticsStateText()
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();

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

    private PhprEffectProfile BuildPhprEffectProfileFromCurrentSettings(string name)
    {
        return PhprEffectProfile.FromAppSettings(
            string.IsNullOrWhiteSpace(name) ? _currentProfile.Name : name,
            new AppSettings
            {
                ShiftIntent = CreateShiftIntentSetting(),
                MockGearPulseRouting = CreateMockGearPulseRoutingSetting(),
                MockPedalEffectsRouting = CreateMockPedalEffectsRoutingSetting(),
                RealPhprGearPulseRouting = CreateRealPhprGearPulseRoutingSetting(),
                RealPhprRoadVibrationRouting = CreateRealPhprRoadVibrationRoutingSetting(),
                RealPhprSlipLockRouting = CreateRealPhprSlipLockRoutingSetting()
            });
    }

    private void ApplyPhprEffectProfileToRuntime(PhprEffectProfile profile)
    {
        var validation = PhprEffectProfileValidator.Validate(profile);
        var settings = validation.Profile.ApplyTo(AppSettings.Default);

        _shiftIntentProcessor.Configure(CreateShiftIntentOptions(settings.ShiftIntent));
        _mockGearPulseRouter.Configure(CreateMockGearPulseRouterOptions(settings.MockGearPulseRouting));
        _mockPedalEffectsRouter.Configure(CreateMockPedalEffectsRouterOptions(settings.MockPedalEffectsRouting));

        var realProfileOptions = CreateRealPhprOutputOptions(settings.RealPhprGearPulseRouting);
        _realPhprOptions = _realPhprOptions.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits) with
        {
            BrakeGearPulse = realProfileOptions.BrakeGearPulse,
            ThrottleGearPulse = realProfileOptions.ThrottleGearPulse
        };
        _realRoadVibrationOptions = CreateRealRoadVibrationRouterOptions(settings.RealPhprRoadVibrationRouting);
        _realSlipLockOptions = CreateRealSlipLockRouterOptions(settings.RealPhprSlipLockRouting);

        _realPhprOutput.Configure(_realPhprOptions);
        _realPhprGearPulseRouter.Configure(_realPhprOptions);
        _realRoadVibrationRouter.Configure(_realRoadVibrationOptions);
        _realSlipLockRouter.Configure(_realSlipLockOptions);

        ApplyShiftIntentSettingsToControls();
        ApplyMockGearPulseSettingsToControls();
        ApplyMockPedalEffectsSettingsToControls();
        ApplyRealPhprOptionsToControls();
        UpdatePhprWorkflowStatus();
        UpdateDeviceStatus();
        UpdateDiagnosticsStatus();
    }

    private static IReadOnlyList<string> CombineValidationMessages(
        IReadOnlyList<string> audioMessages,
        IReadOnlyList<string> phprMessages)
    {
        return audioMessages.Concat(phprMessages).ToArray();
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
        }

        UpdateProfileStatus(
            $"{result.Message} {phprResult.Message}",
            CombineValidationMessages(result.ValidationMessages, phprResult.ValidationMessages));
        FooterStatusText.Text = result.Succeeded && phprResult.Succeeded
            ? $"Saved profiles {Path.GetFileName(result.Path)} and {Path.GetFileName(phprResult.Path)}."
            : $"{result.Message} {phprResult.Message}";
    }

    private async void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _profileStore.LoadAsync(HapticProfileStore.GetDefaultProfilePath());
        var phprResult = await _phprProfileStore.LoadAsync(PhprEffectProfileStore.GetDefaultProfilePath());
        if (result.Succeeded && result.Profile is not null)
        {
            ApplyProfileToControls(result.Profile);
            ApplyProfileToRuntime(result.Profile);
            UpdateEffectStatus();
            UpdateMixerStatus();
        }

        if (phprResult.Succeeded && phprResult.Profile is not null)
        {
            ApplyPhprEffectProfileToRuntime(phprResult.Profile);
            SaveAppSettings();
        }

        UpdateProfileStatus(
            $"{result.Message} {phprResult.Message}",
            CombineValidationMessages(result.ValidationMessages, phprResult.ValidationMessages));
        FooterStatusText.Text = $"{result.Message} {phprResult.Message}";
    }

    private void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyProfileToControls(HapticDriveProfile.Default);
        ApplyProfileToRuntime(HapticDriveProfile.Default);
        ApplyPhprEffectProfileToRuntime(PhprEffectProfile.Default);
        SaveAppSettings();
        UpdateEffectStatus();
        UpdateMixerStatus();
        UpdateProfileStatus("Reset to conservative audio and P-HPR defaults.", []);

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

    private void ManualAsioHardwareTest40HzButton_Click(object sender, RoutedEventArgs e)
    {
        StartManualAsioHardwareTest(40f);
    }

    private void ManualAsioHardwareTest50HzButton_Click(object sender, RoutedEventArgs e)
    {
        StartManualAsioHardwareTest(50f);
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

    private void StartManualAsioHardwareTest(float frequencyHz)
    {
        var durationMs = ManualAsioHardwareDurationComboBox.SelectedItem is int selectedDuration
            ? selectedDuration
            : 250;
        var result = _hapticPipeline.StartManualAsioHardwareTest(
            new ManualAsioHardwareTestRequest(
                frequencyHz,
                TimeSpan.FromMilliseconds(durationMs)));

        FooterStatusText.Text = result.Message;
        UpdateManualAsioHardwareTestStatus();
        UpdateDiagnosticsStatus();
    }

    private async void SelectManualAsioHardwareTestChannel(int channel)
    {
        var wasSelected = _selectedAsioOutputChannel == channel;
        _selectedAsioOutputChannel = channel;
        AsioOutputChannelComboBox.SelectedItem = channel;
        SaveAppSettings();
        if (_selectedOutputKind == AudioOutputDeviceKind.Asio)
        {
            if (wasSelected && _hapticsStarted)
            {
                StartManualAsioHardwareTest(50f);
                return;
            }

            await RebuildHapticPipelineForOutputSelectionAsync($"Manual ASIO Hardware Test selected channel {channel}; haptics are stopped until started explicitly.");
            return;
        }

        UpdateManualAsioHardwareTestStatus();
        FooterStatusText.Text = $"Manual ASIO Hardware Test selected channel {channel}; switch Output mode to ASIO Output before testing.";
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

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            PageStatusText.Text = snapshot.IsActive
                ? $"{snapshot.SelectedSignalName}; {snapshot.RenderedBufferCount:N0} buffer(s); peak {snapshot.OutputPeakLevel:0.000}; limiter {snapshot.LimitedSampleCount:N0} sample(s); mute {(snapshot.IsMuted ? "on" : "off")}; emergency {snapshot.EmergencyMute}."
                : $"Test bench idle; output {snapshot.OutputDisplayName}; mute {(snapshot.IsMuted ? "on" : "off")}; emergency {snapshot.EmergencyMute}.";
        }

        UpdateDiagnosticsStatus();
    }

    private void UpdateManualAsioHardwareTestStatus()
    {
        var snapshot = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        ManualAsioHardwareStatusText.Text =
            $"Manual ASIO Hardware Test: mode {snapshot.TestMode}; driver {snapshot.SelectedAsioDriver}; channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}; running {snapshot.AsioRunning}; armed {snapshot.AsioArmed}; haptics {snapshot.HapticsRunning}; peak {snapshot.OutputPeakLevel:0.000}.";
        ManualAsioHardwareBlockedReasonText.Text = snapshot.BlockedReason is null
            ? $"Last signal {snapshot.LastTestSignal ?? "none"}; duration {(snapshot.LastTestDuration is null ? "none" : $"{snapshot.LastTestDuration.Value.TotalMilliseconds:0} ms")}; submitted {snapshot.FramesSubmitted:N0} frame(s); rendered {snapshot.FramesRendered:N0} manual frame(s); callbacks {snapshot.RenderCallbackCount:N0}; last error {snapshot.LastError ?? "none"}."
            : $"Blocked: {snapshot.BlockedReason}";
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
        OutputModeValueText.Text = status.DisplayName;
        OutputModeDetailText.Text = status.Kind == AudioOutputDeviceKind.Asio
            ? $"{status.StatusMessage} Armed {status.IsHardwareArmed}; channel {(status.SelectedOutputChannel is null ? "not selected" : status.SelectedOutputChannel)}."
            : status.StatusMessage;
        UpdateDeviceStatus();
    }

    private void UpdateMixerStatus()
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        var mixer = _currentProfile.ToMixerSettings(_emergencyMuted);
        var safety = _currentProfile.ToSafetyOptions(_emergencyMuted);
        MasterGainValueText.Text = $"{mixer.MasterGain:P0}";
        SafetyOutputGainValueText.Text = $"{safety.OutputGain:P0}";
        SafetyOutputCeilingValueText.Text = $"{safety.OutputGainCeiling:0.00}";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Routing / Mixer" })
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
        UpdateMockGearPulseStatus();
        UpdateMockPedalEffectsStatus();
        UpdatePhprPedalsStatus();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Devices" })
        {
            PageStatusText.Text = status.RequiresPhysicalHardware
                ? "Selected output requires explicit manual hardware readiness checks; haptics remain stopped until armed and started."
                : $"Hardware-absent mode active; NullAudioOutputDevice remains the safe default; ASIO drivers reported {_asioVisibilitySnapshot.DriverNames.Count}; input devices discovered {(_inputDiscoverySnapshot.HasRun ? _inputDiscoverySnapshot.DeviceCount.ToString("N0") : "not refreshed")}; paddle listener {_paddleInputSource.GetPaddleSnapshot().Status}; shift intent {_shiftIntentProcessor.GetDiagnosticsSnapshot().Mode}; real P-HPR direct control {(_realPhprOptions.DirectControlEnabled ? "enabled" : "disabled")}; real road vibration {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; real slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; mock gear routing {_mockGearPulseRouter.GetSnapshot().Options.TargetModule}; mock pedal effects {(_mockPedalEffectsRouter.GetSnapshot().Options.IsEnabled ? "enabled" : "disabled")}.";
        }
    }

    private void UpdateProfileStatus(string? message = null, IReadOnlyList<string>? validationMessages = null)
    {
        var path = HapticProfileStore.GetDefaultProfilePath();
        var phprPath = PhprEffectProfileStore.GetDefaultProfilePath();
        ProfileStatusText.Text = message ?? $"Active profile: {_currentProfile.Name}.";
        ProfilePathText.Text = $"Audio profile path: {path}";
        ProfilePhprStatusText.Text = $"P-HPR profile path: {phprPath}; profile saves shift intent, mock gear/pedal effects, real gear, road, slip, and lock preferences only. Paddle bench enable is runtime-only.";
        ProfileValidationText.Text = validationMessages is { Count: > 0 }
            ? string.Join(" ", validationMessages)
            : "Profile values are clamped to conservative software ranges on load and save.";
        var shiftIntent = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var mockGear = _mockGearPulseRouter.GetSnapshot().Options;
        var mockPedalEffects = _mockPedalEffectsRouter.GetSnapshot().Options;
        SettingsStatusText.Text = $"Theme: {(_lightTheme ? "Light" : "Dark")}. Active profile: {_currentProfile.Name}. Forwarding destinations {_forwardingDestinations.Count}. Paddle mapping left {FormatButtonMapping(_paddleMapping.LeftPaddleButtonId)}, right {FormatButtonMapping(_paddleMapping.RightPaddleButtonId)}. Shift intent {(shiftIntent.IsEnabled ? "enabled" : "disabled")} mode {shiftIntent.Mode}. Real P-HPR direct control {(_realPhprOptions.DirectControlEnabled ? "enabled" : "disabled")} runtime-only. Real slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}. Mock gear routing {(mockGear.IsEnabled ? "enabled" : "disabled")} target {mockGear.TargetModule}. Mock pedal effects {(mockPedalEffects.IsEnabled ? "enabled" : "disabled")}. Paddle bench enable and manual ASIO test active state are not saved. Default output remains NullAudioOutputDevice. {_settingsError ?? ""}".Trim();
        SettingsPathText.Text = $"App settings path: {_settingsStore.SettingsPath}";
        RuntimePrerequisiteText.Text = $".NET Desktop runtime is available for this running WPF app. Launch script sets DOTNET_ROOT to the repo-local .NET 8 runtime before starting the executable.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Profiles" })
        {
            PageStatusText.Text = $"Active profile {_currentProfile.Name}; audio JSON version {HapticDriveProfile.CurrentVersion}; P-HPR JSON version {PhprEffectProfile.CurrentVersion}; emergency mute and direct-control enable are not saved.";
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
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
        var selectedComboItem = PaddleInputDeviceComboBox.SelectedItem as PaddleDeviceListItem;
        var selectedText = snapshot.SelectedDevice is null
            ? selectedComboItem is not null
                ? $"{selectedComboItem.DisplayText}"
                : _paddleMapping.SelectedDeviceId is null ? "none" : $"saved {_paddleMapping.SelectedDeviceId}"
            : $"{snapshot.SelectedDevice.DisplayName} ({snapshot.SelectedDevice.Method})";
        var lastRaw = snapshot.LastChangedButtonId is null
            ? "none"
            : $"button {snapshot.LastChangedButtonId} {snapshot.LastChangedButtonState}";
        var lastMapped = snapshot.LastPaddleEvent is null
            ? "none"
            : $"{snapshot.LastPaddleEvent.PaddleSide} paddle button {snapshot.LastPaddleEvent.ButtonId} at {snapshot.LastPaddleEvent.TimestampUtc.ToLocalTime():T}";
        var error = snapshot.LastErrorMessage ?? "none";
        var selectionBlocker = BuildPaddleInputSelectionBlocker();
        var selectedButtonCount = selectedComboItem is null
            ? "none"
            : $"{PaddleInputDeviceSelector.GetUsableButtonCount(selectedComboItem.Device):N0}";

        StartPaddleInputListenerButton.IsEnabled = snapshot.Status is not InputListenerStatus.Listening
            and not InputListenerStatus.Starting
            && selectionBlocker is null;
        StartPaddleInputListenerButton.ToolTip = selectionBlocker is null
            ? "Start the read-only Windows game-controller paddle listener."
            : $"Blocked: {selectionBlocker}";
        StopPaddleInputListenerButton.IsEnabled = snapshot.Status is InputListenerStatus.Listening
            or InputListenerStatus.Starting
            or InputListenerStatus.Error
            or InputListenerStatus.Disconnected;
        PaddleInputBadgeText.Text = snapshot.Status is InputListenerStatus.Listening
            ? "Listening"
            : "Listener stopped";
        PaddleInputStatusText.Text =
            $"Paddle listener: {snapshot.Status}; selected {selectedText}; selection {(selectionBlocker is null ? "ready" : $"blocked: {selectionBlocker}")}; mapped presses {snapshot.PaddlePressCount:N0}; last raw {lastRaw}; last mapped {lastMapped}; error {error}.";
        PaddleInputItemsControl.ItemsSource = new[]
        {
            "Safety: mapped paddle presses can feed DrivingArmed-gated shift intent and configured P-HPR routes. Direct hardware output still requires explicit direct-control gates.",
            $"Selected device usable buttons: {selectedButtonCount}; auto-selection prefers a saved usable device, then VID_3670/PID_0905 with at least 32 buttons, then any usable button-capable controller.",
            $"Selected method: {(_paddleMapping.SelectedMethod == InputDiscoveryMethod.Unknown ? InputDiscoveryMethod.WindowsGameController : _paddleMapping.SelectedMethod)}",
            $"Left paddle mapping: {FormatButtonMapping(snapshot.Mapping.LeftPaddleButtonId)}; current state {snapshot.LeftPaddleState}",
            $"Right paddle mapping: {FormatButtonMapping(snapshot.Mapping.RightPaddleButtonId)}; current state {snapshot.RightPaddleState}",
            $"Last changed raw button: {lastRaw}",
            $"Last mapped paddle event: {lastMapped}",
            $"Paddle press count: {snapshot.PaddlePressCount:N0}; debounce suppressed {snapshot.DebounceSuppressedCount:N0}",
            $"Debounce: {snapshot.Mapping.DebounceDuration.TotalMilliseconds:0} ms",
            $"Listener error: {error}"
        };
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
        var diagnostics = _shiftIntentProcessor.GetDiagnosticsSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        var lastAccepted = diagnostics.LastAcceptedEvent is null
            ? "none"
            : $"{diagnostics.LastAcceptedEvent.Direction} at {diagnostics.LastAcceptedEvent.TimestampUtc.ToLocalTime():T}, gear {FormatOptionalInt(diagnostics.LastAcceptedEvent.LastTelemetryGear)}";
        var lastSuppressed = diagnostics.LastSuppressedEvent is null
            ? "none"
            : $"{diagnostics.LastSuppressedEvent.PaddleEvent.PaddleSide} at {diagnostics.LastSuppressedEvent.EvaluatedAtUtc.ToLocalTime():T}: {diagnostics.LastSuppressedEvent.SuppressionReason}";
        var telemetryAge = driving.LastTelemetryAge is null
            ? "none"
            : $"{driving.LastTelemetryAge.Value.TotalMilliseconds:0} ms";

        ShiftIntentStatusText.Text =
            $"Shift intent: {(diagnostics.IsEnabled ? "enabled" : "disabled")}; mode {diagnostics.Mode}; DrivingArmed {driving.Current.IsArmed}; accepted {diagnostics.AcceptedShiftIntentCount:N0}; suppressed {diagnostics.SuppressedShiftIntentCount:N0}; last accepted {lastAccepted}; last suppressed {lastSuppressed}.";
        ShiftIntentItemsControl.ItemsSource = new[]
        {
            "Safety: Stage 2F evaluates mapped paddle intent only. Stage 2M routes accepted events separately to safety-limited mock P-HPR output and still does not touch the ASIO/BST-1 audio path.",
            $"DrivingArmed current state: {driving.Current.IsArmed}; reason {driving.Current.Reason}",
            $"DrivingArmed telemetry age: {telemetryAge}; menu safe mode {driving.MenuSafeModeEnabled}; require recent telemetry {driving.RequireRecentTelemetry}",
            $"Last paddle side: {diagnostics.LastPaddleSide}; direction {diagnostics.LastDirection}; last paddle event {(diagnostics.LastPaddleEvent is null ? "none" : diagnostics.LastPaddleEvent.TimestampUtc.ToLocalTime().ToString("T"))}",
            $"Accepted shift intents: {diagnostics.AcceptedShiftIntentCount:N0}; suppressed shift intents: {diagnostics.SuppressedShiftIntentCount:N0}; observed paddle events {diagnostics.TotalPaddleEventsObserved:N0}",
            $"Last accepted event: {lastAccepted}",
            $"Last suppression reason: {diagnostics.LastSuppressionReason ?? "none"}",
            $"Last known telemetry: gear {FormatOptionalInt(diagnostics.LastTelemetry.LastKnownGear)}, speed {FormatOptionalInt(diagnostics.LastTelemetry.LastKnownSpeedKph)} km/h, RPM {FormatOptionalInt(diagnostics.LastTelemetry.LastKnownRpm)}, frame {FormatOptionalUInt(diagnostics.LastTelemetry.LastKnownFrameIdentifier)}",
            $"Pending confirmation records: {diagnostics.PendingConfirmationCount:N0}; error {diagnostics.LastError ?? "none"}"
        };
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
        var mode = GetPhprWorkflowModeText();
        var realDiagnostics = _realPhprOutput.GetDiagnostics();
        var mockGear = _mockGearPulseRouter.GetSnapshot();
        var mockPedalEffects = _mockPedalEffectsRouter.GetSnapshot();
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var validation = _phprManualValidationReadiness;
        var liveValidation = PhprLiveF1ValidationGuide.Build(
            BuildPhprLiveF1ValidationSnapshot(pipelineSnapshot, realDiagnostics));
        var selectedDevice = realDiagnostics.Options.Selector.IsSelected
            ? "selected for this session"
            : "not selected";
        var replaySource = FormatReplaySource(pipelineSnapshot);
        var warning = realDiagnostics.Options.DirectControlEnabled
            ? "Real direct-control state is runtime-only; profile/app settings do not save enable, private device path, emergency stop, or write history."
            : "Real direct control is currently disabled; mock routing and diagnostics remain hardware-safe.";

        PhprWorkflowStatusText.Text =
            $"P-HPR mode: {mode}; telemetry input {pipelineSnapshot.InputSource}; replay source {replaySource}; selected output {selectedDevice}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}; direct control {(realDiagnostics.Options.DirectControlEnabled ? "enabled" : "disabled")}; emergency stop {realDiagnostics.Output.IsEmergencyStopActive}; validation {(validation.IsBlocked ? "blocked" : "ready")}.";
        PhprWorkflowItemsControl.ItemsSource = new[]
        {
            warning,
            $"Replay validation: input {pipelineSnapshot.InputSource}; replay source {replaySource}; replay packets {pipelineSnapshot.Replay.PacketsReplayed:N0}; replay does not synthesize gear-paddle events.",
            $"Profiles: audio {Path.GetFileName(HapticProfileStore.GetDefaultProfilePath())}; P-HPR {Path.GetFileName(PhprEffectProfileStore.GetDefaultProfilePath())}; P-HPR profile saves effect preferences only.",
            $"Instant gear pulse: brake {FormatRealPhprPulse(realDiagnostics.Options.BrakeGearPulse)}; throttle {FormatRealPhprPulse(realDiagnostics.Options.ThrottleGearPulse)}; last latency {BuildRealPhprGearPulseLatencyText()}.",
            $"Road vibration: {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; brake {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Brake)}; throttle {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Throttle)}; last {BuildRealRoadVibrationRoutingText()}.",
            $"Slip/lock: {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; slip {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip)}; lock {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock)}; last {BuildRealSlipLockRoutingText()}.",
            $"Mock routing: gear {(mockGear.Options.IsEnabled ? "enabled" : "disabled")} target {mockGear.Options.TargetModule}; pedal effects {(mockPedalEffects.Options.IsEnabled ? "enabled" : "disabled")}; shared mock commands {mockGear.OutputSnapshot.AcceptedCommandCount:N0}; pending stops {mockGear.OutputSnapshot.PendingScheduledStopCount:N0}.",
            $"Real output counters: writes {realDiagnostics.ReportWriteCount:N0}; failures {realDiagnostics.FailedReportWriteCount:N0}; connection {realDiagnostics.Connection.State}; last error {realDiagnostics.LastError ?? "none"}."
        };
        PhprLiveF1ValidationStatusText.Text = liveValidation.Summary;
        PhprLiveF1ValidationItemsControl.ItemsSource = liveValidation.Checklist;
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
        var canPulse = options.DirectControlEnabled
            && !options.CandidateIsRawInputOnly
            && options.CandidateHasOpenableHidPath
            && options.OpenCheckSucceeded
            && options.AllowsDirectPulseReportShape
            && selector.IsSelected
            && coexistenceClear
            && !diagnostics.Output.IsEmergencyStopActive;
        TestRealPhprBrakePulseButton.IsEnabled = canPulse && options.BrakeGearPulse.IsEnabled;
        TestRealPhprThrottlePulseButton.IsEnabled = canPulse && options.ThrottleGearPulse.IsEnabled;
        RealPhprDirectStatusText.Text =
            $"Real direct control: {(options.DirectControlEnabled ? "enabled" : "disabled")}; {(canPulse ? "Direct ready" : "Direct blocked")}; device {(selector.IsSelected ? "selected" : "not selected")}; road {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; connection {diagnostics.Connection.State}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}; emergency stop {diagnostics.Output.IsEmergencyStopActive}; report writes {diagnostics.ReportWriteCount:N0}; failures {diagnostics.FailedReportWriteCount:N0}.";
        RealPhprDirectItemsControl.ItemsSource = new[]
        {
            "Safety: write-capable direct path. Direct enable, selected private device path, emergency stop, and write history are runtime-only and are not persisted.",
            $"Selected interface: {selector.InterfaceName}; transport {selector.Transport}; report ID {FormatReportId(selector.ReportId)}; report length {selector.ReportLength:N0} byte(s); expected first bytes {PHprHidReportShapeValidationResult.ExpectedF1EcStartFirstBytes}; private path {(selector.IsSelected ? "held in memory only" : "none")}.",
            $"Direct-output candidate picker: {_realPhprCandidateItems.Count:N0} refreshed candidate(s); source {options.CandidateSourceMethod}; raw-input-only {options.CandidateIsRawInputOnly}; openable HID path {options.CandidateHasOpenableHidPath}; output report known {options.CandidateOutputReportCapabilityKnown}; feature report known {options.CandidateFeatureReportCapabilityKnown}; report-shape attempted {options.ReportShapeValidationAttempted}; succeeded {options.ReportShapeValidationSucceeded}; failed {options.ReportShapeValidationFailed}; message {options.ReportShapeValidationMessage ?? "none"}; open-check attempted {options.OpenCheckAttempted}; succeeded {options.OpenCheckSucceeded}; failed {options.OpenCheckFailed}; sanitized open error {options.OpenCheckSanitizedErrorCategory ?? "none"}.",
            $"Lifecycle: connection {diagnostics.Connection.State}; writer open {diagnostics.Connection.WriterOpen}; opens {diagnostics.Connection.OpenSuccessCount:N0}/{diagnostics.Connection.OpenAttemptCount:N0}; closes {diagnostics.Connection.CloseSuccessCount:N0}/{diagnostics.Connection.CloseAttemptCount:N0}; timeout {options.WriteTimeoutMs:N0} ms.",
            $"Brake pulse: {(options.BrakeGearPulse.IsEnabled ? "enabled" : "disabled")}; strength {PhprUiValueConverter.FormatPercent(options.BrakeGearPulse.Strength01)}%; frequency {PhprUiValueConverter.FormatFrequency(options.BrakeGearPulse.FrequencyHz)} Hz; duration {options.BrakeGearPulse.DurationMs} ms.",
            $"Throttle pulse: {(options.ThrottleGearPulse.IsEnabled ? "enabled" : "disabled")}; strength {PhprUiValueConverter.FormatPercent(options.ThrottleGearPulse.Strength01)}%; frequency {PhprUiValueConverter.FormatFrequency(options.ThrottleGearPulse.FrequencyHz)} Hz; duration {options.ThrottleGearPulse.DurationMs} ms.",
            $"Road vibration: {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; brake {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Brake)}; throttle {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Throttle)}; last {BuildRealRoadVibrationRoutingText()}.",
            $"Slip/lock: {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; slip {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip)}; lock {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock)}; last {BuildRealSlipLockRoutingText()}.",
            $"Manual pulse buttons: {(canPulse ? "available" : "blocked")}; requires enabled direct control, selected HID device-interface candidate, successful open-check, selected output/feature report capability, successful no-command report-shape validation, clear coexistence, and no emergency stop latch.",
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
            $"Replay: {(replaySnapshot.IsReplaying ? "active" : "inactive")}; source {FormatReplaySource(pipelineSnapshot)}; {replaySnapshot.PacketsReplayed:N0} packet(s); {replaySnapshot.StatusMessage}",
            $"Effects: enabled engine {effectSnapshot.Engine.IsEnabled}, gear {effectSnapshot.GearShift.IsEnabled}, kerb {effectSnapshot.Kerb.IsEnabled}, impact {effectSnapshot.Impact.IsEnabled}, road {effectSnapshot.RoadTexture.IsEnabled}, slip {effectSnapshot.Slip.IsEnabled}; peak {effectSnapshot.PeakLevel:0.000}.",
            $"Mixer / safety: mixer peak {audioDiagnostics.MixerPeakLevel:0.000}; output peak {audioDiagnostics.OutputPeakLevel:0.000}; limited {audioDiagnostics.LimitedSampleCount:N0}; clipped {audioDiagnostics.ClippedSampleCount:N0}; emergency mute {audioDiagnostics.EmergencyMute}.",
            $"Test bench: {(testBenchSnapshot.IsActive ? "active" : "inactive")}; signal {testBenchSnapshot.SelectedSignalName}; output {testBenchSnapshot.OutputDisplayName}; peak {testBenchSnapshot.OutputPeakLevel:0.000}.",
            $"Output: {outputStatus.DisplayName} ({outputStatus.State}); streaming {outputStatus.IsStreaming}; hardware required {outputStatus.RequiresPhysicalHardware}; manual debug {outputStatus.IsManualDebugOnly}; hardware-absent mode {audioDiagnostics.HardwareAbsentMode}; null buffers {pipelineSnapshot.NullOutput?.SubmittedBufferCount ?? 0:N0}; render callbacks {outputStatus.RenderCallbackCount:N0}; backend callbacks {outputStatus.BackendCallbackCount:N0}; output buffers {outputStatus.SubmittedBufferCount:N0}; drops {outputStatus.DroppedBufferCount:N0}; underruns {outputStatus.UnderrunCount:N0}; render {FormatDuration(outputStatus.LastRenderDuration)}; jitter {FormatDuration(outputStatus.LastCallbackJitter)}.",
            $"Input discovery: {BuildInputDiscoveryDiagnosticsText()}",
            $"Paddle input listener: {BuildPaddleInputDiagnosticsText()}",
            $"Shift intent layer: {BuildShiftIntentDiagnosticsText()}",
            PhprWorkflowDiagnosticsReport.BuildProfilePersistenceLine(
                HapticProfileStore.GetDefaultProfilePath(),
                PhprEffectProfileStore.GetDefaultProfilePath()),
            PhprWorkflowDiagnosticsReport.BuildWorkflowLine(new PhprWorkflowDiagnosticsSnapshot(
                GetPhprWorkflowModeText(),
                pipelineSnapshot.InputSource.ToString(),
                FormatReplaySource(pipelineSnapshot),
                pipelineSnapshot.Replay.PacketsReplayed,
                _realPhprOptions.DirectControlEnabled,
                _realPhprOptions.DirectControlArmed,
                _realPhprOptions.Selector.IsSelected,
                _mockGearPulseRouter.GetSnapshot().Options.IsEnabled,
                _mockPedalEffectsRouter.GetSnapshot().Options.IsEnabled,
                _realRoadVibrationOptions.IsEnabled,
                _realSlipLockOptions.IsEnabled)),
            PhprLiveF1ValidationGuide.Build(BuildPhprLiveF1ValidationSnapshot(
                pipelineSnapshot,
                _realPhprOutput.GetDiagnostics())).DiagnosticsLine,
            $"P-HPR software coexistence: {BuildPhprCoexistenceDiagnosticsText()}",
            $"P-HPR direct write readiness: {BuildPhprControlledWriteReadinessDiagnosticsText()}",
            $"P-HPR real direct control: {BuildRealPhprDirectDiagnosticsText()}",
            $"P-HPR validation harness: {BuildPhprValidationDiagnosticsText()}",
            $"Paddle gear bench test: {BuildPaddleGearBenchDiagnosticsText()}",
            $"Mock P-HPR gear routing: {BuildMockGearPulseDiagnosticsText()}",
            $"Mock P-HPR pedal effects: {BuildMockPedalEffectsDiagnosticsText()}",
            $"Manual ASIO Hardware Test: {BuildManualAsioHardwareTestDiagnosticsText()}",
            $"ASIO readiness: {_asioReadinessSnapshot.Message} Drivers reported {_asioReadinessSnapshot.DriverNames.Count}; M-Audio match {(_asioReadinessSnapshot.MTrackDriverVisible ? "yes" : "no")}; channel {(_asioReadinessSnapshot.SelectedOutputChannel is null ? "none" : _asioReadinessSnapshot.SelectedOutputChannel)}; armed {_asioReadinessSnapshot.IsArmed}; Windows sound output proves ASIO {_asioReadinessSnapshot.WindowsSoundOutputVisibilityProvesAsio}.",
            $"Runtime prerequisites: .NET {Environment.Version}; WPF desktop runtime is present because the app is running; launch script sets DOTNET_ROOT to the repo-local runtime before starting the executable.",
            $"App settings: {_settingsStore.SettingsPath}; {(_settingsError ?? "loaded")}; theme {(_lightTheme ? "light" : "dark")}; persisted ASIO driver {(_selectedAsioDriverName ?? "none")}; persisted ASIO channel {(_selectedAsioOutputChannel is null ? "none" : _selectedAsioOutputChannel)}; persisted paddle mapping device {_paddleMapping.SelectedDeviceId ?? "none"} left {FormatButtonMapping(_paddleMapping.LeftPaddleButtonId)} right {FormatButtonMapping(_paddleMapping.RightPaddleButtonId)}; shift intent {(_shiftIntentProcessor.GetDiagnosticsSnapshot().IsEnabled ? "enabled" : "disabled")} mode {_shiftIntentProcessor.GetDiagnosticsSnapshot().Mode}; mock gear routing {(_mockGearPulseRouter.GetSnapshot().Options.IsEnabled ? "enabled" : "disabled")} target {_mockGearPulseRouter.GetSnapshot().Options.TargetModule}; mock pedal effects {(_mockPedalEffectsRouter.GetSnapshot().Options.IsEnabled ? "enabled" : "disabled")}; real road vibration {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")}; real slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; ASIO armed state, haptics running state, emergency mute, P-HPR real direct-control enabled/selected device, P-HPR emergency stop state, safety latch state, paddle bench enable state, manual ASIO test active state, and mock histories are not persisted."
        };

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
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
        return $"{(options.DirectControlEnabled ? "enabled" : "disabled")}; selected {selector.IsSelected}; candidates {_realPhprCandidateItems.Count:N0}; source {options.CandidateSourceMethod}; raw-input-only {options.CandidateIsRawInputOnly}; openable {options.CandidateHasOpenableHidPath}; transport {selector.Transport}; output report known {options.CandidateOutputReportCapabilityKnown}; feature report known {options.CandidateFeatureReportCapabilityKnown}; report-shape attempted {options.ReportShapeValidationAttempted}; succeeded {options.ReportShapeValidationSucceeded}; failed {options.ReportShapeValidationFailed}; shape message {options.ReportShapeValidationMessage ?? "none"}; expected first bytes {PHprHidReportShapeValidationResult.ExpectedF1EcStartFirstBytes}; open-check attempted {options.OpenCheckAttempted}; succeeded {options.OpenCheckSucceeded}; failed {options.OpenCheckFailed}; open error {options.OpenCheckSanitizedErrorCategory ?? "none"}; connection {diagnostics.Connection.State}; writer open {diagnostics.Connection.WriterOpen}; interface {selector.InterfaceName}; report ID {FormatReportId(selector.ReportId)}; report length {selector.ReportLength:N0}; private path {(selector.IsSelected ? "held in memory only" : "none")}; timeout {options.WriteTimeoutMs:N0} ms; can pulse {canPulse}; brake {FormatRealPhprPulse(options.BrakeGearPulse)}; throttle {FormatRealPhprPulse(options.ThrottleGearPulse)}; road {(_realRoadVibrationOptions.IsEnabled ? "enabled" : "disabled")} brake {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Brake)} throttle {FormatRealRoadVibrationPedal(_realRoadVibrationOptions.Throttle)} last road {BuildRealRoadVibrationRoutingText()}; slip/lock {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")} slip {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip)} lock {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock)} last slip/lock {BuildRealSlipLockRoutingText()}; gear latency {BuildRealPhprGearPulseLatencyText()}; writes {diagnostics.ReportWriteCount:N0}; failures {diagnostics.FailedReportWriteCount:N0}; opens {diagnostics.Connection.OpenSuccessCount:N0}/{diagnostics.Connection.OpenAttemptCount:N0}; closes {diagnostics.Connection.CloseSuccessCount:N0}/{diagnostics.Connection.CloseAttemptCount:N0}; stops {diagnostics.Connection.StopReportWriteCount:N0}; disconnects {diagnostics.Connection.DisconnectCount:N0}; timeouts {diagnostics.Connection.TimeoutCount:N0}; invalid reports {diagnostics.Connection.InvalidReportCount:N0}; last target {diagnostics.LastTarget?.ToString() ?? "none"}; last report {diagnostics.LastReportState?.ToString() ?? "none"} {diagnostics.LastReportLength:N0} bytes; last status {diagnostics.Output.LastStatus?.ToString() ?? "none"}; write {diagnostics.Connection.LastWriteStatus?.ToString() ?? "none"}; stop {diagnostics.Connection.LastStopStatus?.ToString() ?? "none"}; open {diagnostics.Connection.LastOpenStatus?.ToString() ?? "none"}; close {diagnostics.Connection.LastCloseStatus?.ToString() ?? "none"}; last error {diagnostics.LastError ?? "none"}; runtime-only enable/open-check/device not persisted; safe gear-pulse, road, and slip/lock settings persisted.";
    }

    private string BuildRealRoadVibrationRoutingText()
    {
        var result = _lastRealRoadVibrationRoutingResult;
        return result is null
            ? "none"
            : $"{result.Status}; routed {result.WasRouted}; intensity {result.Intensity01:0.###}; commands {result.Commands.Count:N0}; at {FormatTimestamp(result.RoutedAtUtc)}";
    }

    private string BuildRealSlipLockRoutingText()
    {
        var result = _lastRealSlipLockRoutingResult;
        if (result is null)
        {
            return "none";
        }

        var kinds = result.Commands.Count == 0
            ? "none"
            : string.Join(
                ", ",
                result.Commands.Select(command => $"{command.Kind}->{command.TargetModule}"));
        return $"{result.Status}; routed {result.WasRouted}; commands {result.Commands.Count:N0}; {kinds}; at {FormatTimestamp(result.RoutedAtUtc)}";
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
        return $"mode {snapshot.TestMode}; active {snapshot.IsActive}; driver {snapshot.SelectedAsioDriver}; channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}; ASIO running {snapshot.AsioRunning}; armed {snapshot.AsioArmed}; haptics {snapshot.HapticsRunning}; emergency {snapshot.EmergencyMute}; normal mute {snapshot.NormalMute}; peak {snapshot.OutputPeakLevel:0.000}; submitted {snapshot.FramesSubmitted:N0} frame(s); rendered {snapshot.FramesRendered:N0} manual frame(s); callbacks {snapshot.RenderCallbackCount:N0}; blocked {snapshot.BlockedReason ?? "none"}; last signal {snapshot.LastTestSignal ?? "none"}; duration {(snapshot.LastTestDuration is null ? "none" : $"{snapshot.LastTestDuration.Value.TotalMilliseconds:0} ms")}; error {snapshot.LastError ?? "none"}.";
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
        return $"{(normalized.IsEnabled ? "on" : "off")} target {normalized.TargetModule}; strength {PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01)}-{PhprUiValueConverter.FormatPercent(normalized.Strength01)}%; freq {PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz)}-{PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz)} Hz; duration {normalized.DurationMs} ms";
    }

    private static TimeSpan? DurationBetween(DateTimeOffset? start, DateTimeOffset? end)
    {
        return start is null || end is null ? null : end.Value - start.Value;
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "none" : timestamp.Value.ToString("O", CultureInfo.InvariantCulture);
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

    private async void TelemetryStatusTimer_Tick(object? sender, EventArgs e)
    {
        RefreshPhprSoftwareCoexistenceStatus();
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        await RouteMockPedalEffectsFromSnapshotAsync(pipelineSnapshot);
        var slipLockResult = await RouteRealSlipLockFromSnapshotAsync(pipelineSnapshot);
        await RouteRealRoadVibrationFromSnapshotAsync(pipelineSnapshot, slipLockResult?.WasRouted == true);
        UpdateTelemetryStatus();
        UpdateHapticsStateText();
        UpdateMixerStatus();
        UpdateOutputStatus(_hapticPipeline.GetSnapshot().Output);
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
        var canPulse = options.DirectControlEnabled
            && !options.CandidateIsRawInputOnly
            && options.CandidateHasOpenableHidPath
            && options.OpenCheckSucceeded
            && options.AllowsDirectPulseReportShape
            && options.Selector.IsSelected
            && _phprSoftwareCoexistenceSnapshot.Status == PHprSoftwareConflictStatus.Clear
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
            || pipelineSnapshot.VehicleStateUpdateCount <= 0)
        {
            return;
        }

        _routingMockPedalEffects = true;
        try
        {
            await _mockPedalEffectsRouter.RouteAsync(
                pipelineSnapshot,
                BuildMockPedalEffectsSafetyContext(pipelineSnapshot));
        }
        finally
        {
            _routingMockPedalEffects = false;
        }
    }

    private async Task<PHprSlipLockRoutingResult?> RouteRealSlipLockFromSnapshotAsync(HapticPipelineSnapshot pipelineSnapshot)
    {
        if (_routingRealSlipLock)
        {
            return null;
        }

        if (!_realSlipLockRouter.GetSnapshot().Options.IsEnabled
            || !_realPhprOptions.DirectControlEnabled
            || _realPhprOptions.CandidateIsRawInputOnly
            || !_realPhprOptions.CandidateHasOpenableHidPath
            || !_realPhprOptions.OpenCheckSucceeded
            || !_realPhprOptions.AllowsDirectPulseReportShape
            || !_realPhprOptions.Selector.IsSelected
            || pipelineSnapshot.VehicleStateUpdateCount <= 0)
        {
            return null;
        }

        _routingRealSlipLock = true;
        try
        {
            var result = await _realSlipLockRouter.RouteAsync(
                pipelineSnapshot.VehicleState,
                BuildRealSlipLockSafetyContext(pipelineSnapshot));
            _lastRealSlipLockRoutingResult = result;
            return result;
        }
        finally
        {
            _routingRealSlipLock = false;
        }
    }

    private async Task RouteRealRoadVibrationFromSnapshotAsync(
        HapticPipelineSnapshot pipelineSnapshot,
        bool higherPriorityPedalEffectRouted)
    {
        if (_routingRealRoadVibration)
        {
            return;
        }

        if (higherPriorityPedalEffectRouted
            || !_realRoadVibrationRouter.GetSnapshot().Options.IsEnabled
            || !_realPhprOptions.DirectControlEnabled
            || _realPhprOptions.CandidateIsRawInputOnly
            || !_realPhprOptions.CandidateHasOpenableHidPath
            || !_realPhprOptions.OpenCheckSucceeded
            || !_realPhprOptions.AllowsDirectPulseReportShape
            || !_realPhprOptions.Selector.IsSelected
            || pipelineSnapshot.VehicleStateUpdateCount <= 0)
        {
            return;
        }

        _routingRealRoadVibration = true;
        try
        {
            var result = await _realRoadVibrationRouter.RouteAsync(
                pipelineSnapshot.VehicleState,
                BuildRealRoadVibrationSafetyContext(pipelineSnapshot));
            _lastRealRoadVibrationRoutingResult = result;
        }
        finally
        {
            _routingRealRoadVibration = false;
        }
    }

    private void PaddleInputSource_InputChanged(object? sender, object e)
    {
        _ = RunOnUiSafelyAsync("paddle-input-status-refresh", stopAllIfPulseMayHaveStarted: false, () =>
        {
            UpdatePaddleInputStatus();
            UpdateDiagnosticsStatus();
        });
    }

    private async void PaddleInputSource_PaddleInputReceived(object? sender, WheelPaddleInputEvent e)
    {
        var stopAllIfPulseMayHaveStarted = false;
        try
        {
            var result = _shiftIntentProcessor.HandlePaddleInput(e);
            PHprGearPulseRoutingResult? routingResult = null;
            PHprDirectGearPulseRoutingResult? realRoutingResult = null;
            var benchResult = _paddleGearBenchTestController.HandlePaddleInput(e, _paddleMapping);
            string? benchRoutingMessage = null;
            if (result.WasAccepted && result.ShiftIntentEvent is not null)
            {
                routingResult = await _mockGearPulseRouter.RouteAsync(
                    result.ShiftIntentEvent,
                    BuildMockGearPulseSafetyContext(result.ShiftIntentEvent));
                realRoutingResult = await _realPhprGearPulseRouter.RouteAsync(
                    result.ShiftIntentEvent,
                    BuildRealGearPulseSafetyContext(result.ShiftIntentEvent));
            }

            if (benchResult.Accepted && benchResult.ShiftIntentEvent is not null)
            {
                stopAllIfPulseMayHaveStarted =
                    benchResult.Options.Normalize().OutputMode == PaddleGearBenchTestOutputMode.Direct;
                benchRoutingMessage = await RoutePaddleGearBenchAsync(benchResult);
            }

            await RunOnUiAsync(() =>
            {
                if (realRoutingResult is not null)
                {
                    _lastRealPhprGearPulseRoutingResult = realRoutingResult;
                }

                UpdateShiftIntentStatus();
                UpdatePaddleGearBenchStatus();
                UpdateRealPhprDirectControlStatus();
                UpdatePhprValidationStatus();
                UpdateMockGearPulseStatus();
                UpdateMockPedalEffectsStatus();
                UpdateDiagnosticsStatus();

                var realMessage = realRoutingResult is not null
                    && (_realPhprOptions.DirectControlEnabled || realRoutingResult.Routed)
                        ? $" {realRoutingResult.Message}"
                        : string.Empty;
                FooterStatusText.Text = routingResult is null
                    ? $"{result.Message}{realMessage}{FormatBenchRoutingFooter(benchResult, benchRoutingMessage)}"
                    : $"{result.Message} {routingResult.Message}{realMessage}{FormatBenchRoutingFooter(benchResult, benchRoutingMessage)}";
            });
        }
        catch (Exception ex)
        {
            await HandlePaddleInputEventExceptionAsync(
                "paddle-input-event-exception",
                ex,
                stopAllIfPulseMayHaveStarted);
        }
    }

    private async Task<string> RoutePaddleGearBenchAsync(PaddleGearBenchTestResult benchResult)
    {
        var options = benchResult.Options.Normalize();
        if (benchResult.ShiftIntentEvent is null)
        {
            _paddleGearBenchTestController.RecordOutputStatus("No bench shift event was available.");
            return "Bench routing skipped: no bench shift event was available.";
        }

        if (options.OutputMode == PaddleGearBenchTestOutputMode.Mock)
        {
            var routeOptions = new PHprGearPulseRouterOptions
            {
                IsEnabled = true,
                TargetModule = options.TargetModule,
                Profile = options.Profile
            }.Normalize();
            var result = await _mockGearPulseRouter.RouteAsync(
                benchResult.ShiftIntentEvent,
                routeOptions,
                BuildPaddleGearBenchMockSafetyContext());
            var message = $"Bench Mock: {result.Message}";
            _paddleGearBenchTestController.RecordOutputStatus(message);
            return message;
        }

        var directMessage = await RoutePaddleGearBenchDirectAsync(benchResult, options);
        _paddleGearBenchTestController.RecordOutputStatus(directMessage);
        return directMessage;
    }

    private async Task<string> RoutePaddleGearBenchDirectAsync(
        PaddleGearBenchTestResult benchResult,
        PaddleGearBenchTestOptions options)
    {
        if (!await ApplyPhprPedalsNormalOptionsFromControlsAsync(
                "P-HPR pedal settings applied for Paddle Gear Bench Test."))
        {
            return "Bench Direct blocked: Devices-tab pedal card settings are invalid.";
        }

        ConfigurePhprDirectRuntime();
        var message = await _phprDirectRuntime.RouteBenchAsync(
            benchResult,
            options,
            _paddleInputSource.GetPaddleSnapshot(),
            GetDeviceCardPulseSettings,
            BuildPaddleGearBenchDirectSafetyContext());
        UpdateRealPhprDirectControlStatus();
        UpdatePhprValidationStatus();
        return message;
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
        bool stopAllIfPulseMayHaveStarted,
        Action action)
    {
        try
        {
            await RunOnUiAsync(action);
        }
        catch (Exception ex)
        {
            await HandlePaddleInputEventExceptionAsync(reason, ex, stopAllIfPulseMayHaveStarted);
        }
    }

    private async Task HandlePaddleInputEventExceptionAsync(
        string reason,
        Exception exception,
        bool stopAllIfPulseMayHaveStarted)
    {
        try
        {
            await _phprDirectRuntime.HandlePaddleInputExceptionAsync(
                reason,
                exception,
                stopAllIfPulseMayHaveStarted);
        }
        catch
        {
            WritePaddleGearBenchCrashLog($"{reason}-recovery-failed", exception);
        }

        try
        {
            await RunOnUiAsync(() =>
            {
                UpdatePaddleGearBenchStatus();
                UpdateRealPhprDirectControlStatus();
                UpdatePhprValidationStatus();
                UpdateDiagnosticsStatus();
                FooterStatusText.Text = "Paddle input routing failed safely; P-HPR Stop All recovery was attempted when needed.";
            });
        }
        catch (Exception uiException)
        {
            await _phprDirectRuntime.HandlePaddleInputExceptionAsync(
                $"{reason}-status-update-failed",
                uiException,
                stopAllIfPulseMayHaveStarted: false);
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

    private static string FormatBenchRoutingFooter(
        PaddleGearBenchTestResult benchResult,
        string? routingMessage)
    {
        if (benchResult.Accepted)
        {
            return string.IsNullOrWhiteSpace(routingMessage)
                ? " Bench event accepted."
                : $" {routingMessage}";
        }

        return $" {benchResult.Message}";
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

    private HapticPipelineSnapshot RefreshDrivingArmedAndShiftIntentTelemetry()
    {
        var snapshot = _hapticPipeline.GetSnapshot();
        _drivingArmedStateService.UpdateFromPipelineSnapshot(snapshot);
        _shiftIntentProcessor.UpdateFromPipelineSnapshot(snapshot);
        return snapshot;
    }

    private PHprSafetyContext BuildMockGearPulseSafetyContext(ShiftIntentEvent shiftIntentEvent)
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        return BuildMockPhprSafetyContext(
            pipelineSnapshot,
            shiftIntentEvent.DrivingArmedAtEvent.IsArmed);
    }

    private PHprSafetyContext BuildMockPedalEffectsSafetyContext(HapticPipelineSnapshot pipelineSnapshot)
    {
        var driving = _drivingArmedStateService.GetSnapshot();
        return BuildMockPhprSafetyContext(pipelineSnapshot, driving.Current.IsArmed);
    }

    private PHprSafetyContext BuildRealGearPulseSafetyContext(ShiftIntentEvent shiftIntentEvent)
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: false,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
            HapticsStopped: !pipelineSnapshot.IsRunning,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: shiftIntentEvent.DrivingArmedAtEvent.IsArmed,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status,
            RequiresRealDeviceWrites: true);
    }

    private PHprSafetyContext BuildPaddleGearBenchMockSafetyContext()
    {
        var outputSnapshot = _mockPhprSafetyOutput.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: true,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: false,
            HapticsStopped: false,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: true,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: PHprSoftwareConflictStatus.Clear,
            RequiresRealDeviceWrites: false);
    }

    private PHprSafetyContext BuildPaddleGearBenchDirectSafetyContext()
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: false,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: false,
            HapticsStopped: false,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: true,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status,
            RequiresRealDeviceWrites: true);
    }

    private PHprSafetyContext BuildRealRoadVibrationSafetyContext(HapticPipelineSnapshot pipelineSnapshot)
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: false,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
            HapticsStopped: !pipelineSnapshot.IsRunning,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: driving.Current.IsArmed,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status,
            RequiresRealDeviceWrites: true);
    }

    private PHprSafetyContext BuildRealSlipLockSafetyContext(HapticPipelineSnapshot pipelineSnapshot)
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: false,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
            HapticsStopped: !pipelineSnapshot.IsRunning,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: driving.Current.IsArmed,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status,
            RequiresRealDeviceWrites: true);
    }

    private PHprSafetyContext BuildManualRealPhprSafetyContext()
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: false,
            IsDeviceConnected: _realPhprOptions.Selector.IsSelected,
            BrakeModuleAvailable: _realPhprOptions.Selector.IsSelected,
            ThrottleModuleAvailable: _realPhprOptions.Selector.IsSelected,
            TelemetryStale: false,
            HapticsStopped: false,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: true,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status,
            RequiresRealDeviceWrites: true);
    }

    private PHprSafetyContext BuildMockPhprSafetyContext(
        HapticPipelineSnapshot pipelineSnapshot,
        bool drivingArmed)
    {
        var outputSnapshot = _mockPhprSafetyOutput.GetSnapshot();
        return new PHprSafetyContext(
            IsMockOutput: true,
            IsDeviceConnected: outputSnapshot.IsConnected,
            BrakeModuleAvailable: outputSnapshot.BrakeAvailable,
            ThrottleModuleAvailable: outputSnapshot.ThrottleAvailable,
            TelemetryStale: pipelineSnapshot.TelemetryTimedOutMuted,
            HapticsStopped: !pipelineSnapshot.IsRunning,
            EmergencyMuteActive: _emergencyMuted,
            DrivingArmed: drivingArmed,
            EmergencyStopActive: outputSnapshot.IsEmergencyStopActive,
            SoftwareConflictStatus: _phprSoftwareCoexistenceSnapshot.Status,
            RequiresRealDeviceWrites: false);
    }

    private void UpdateTelemetryStatus()
    {
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
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

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Telemetry / UDP" })
        {
            var forwardingSnapshot = pipelineSnapshot.Forwarding;
            var parsedPackets = pipelineSnapshot.ParserSuccessCount;
            var vehicleStateUpdates = pipelineSnapshot.VehicleStateUpdateCount;
            var recordingSnapshot = pipelineSnapshot.Recording;
            PageStatusText.Text = $"{status} on port {snapshot.BoundPort}; forwarding {forwardingSnapshot.ForwardedDatagramCount:N0} datagrams; recording {recordingSnapshot.PacketCount:N0} packets; parsed {parsedPackets:N0} packets; VehicleState {vehicleStateUpdates:N0} updates";
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

    private static string GetLocalValidationResultsDirectory()
    {
        var repoRoot = FindRepositoryRoot();
        return repoRoot is null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HapticDrive.Asio",
                "local-validation-results")
            : Path.Combine(repoRoot, "local-validation-results");
    }

    private void WritePaddleGearBenchCrashLog(string reason, Exception? exception)
    {
        _phprDirectRuntime.HandleUnhandledException(reason, exception);
    }

    private static string? TryReadGitHeadSummary()
    {
        var repoRoot = FindRepositoryRoot();
        if (repoRoot is null)
        {
            return null;
        }

        try
        {
            var headPath = Path.Combine(repoRoot, ".git", "HEAD");
            if (!File.Exists(headPath))
            {
                return null;
            }

            var head = File.ReadAllText(headPath).Trim();
            if (head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
            {
                var refPath = head["ref:".Length..].Trim().Replace('/', Path.DirectorySeparatorChar);
                var fullRefPath = Path.Combine(repoRoot, ".git", refPath);
                if (File.Exists(fullRefPath))
                {
                    var sha = File.ReadAllText(fullRefPath).Trim();
                    return sha.Length >= 7 ? sha[..7] : sha;
                }
            }

            return head.Length >= 7 ? head[..7] : head;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispatcher.UnhandledException -= MainWindow_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        _telemetryStatusTimer.Stop();
        _testBench.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _paddleInputSource.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _telemetryReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _realPhprOutput.DisposeAsync().AsTask().GetAwaiter().GetResult();
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

    private enum PhprPedalsMode
    {
        Disabled = 0,
        Mock = 1,
        Direct = 2
    }

    private sealed record PhprPedalsModeOption(
        PhprPedalsMode Mode,
        string Label);

    private sealed record ForwardingDestinationListItem(
        int Index,
        ForwardingDestinationSetting Setting,
        string DisplayText);

    private sealed record PaddleDeviceListItem(
        InputDeviceInfo Device,
        InputDeviceSelection Selection,
        string DisplayText);

    private sealed record PhprDirectOutputCandidateListItem(
        int Index,
        PHprDirectOutputCandidate Candidate,
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
