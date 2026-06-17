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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
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
    private readonly PHprContinuousEffectsRuntimeCoordinator _realPhprContinuousEffectsRuntime;
    private readonly PaddleInputRoutingCoordinator _paddleInputRoutingCoordinator;
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
    private readonly IReadOnlyList<PHprGearPulseTarget> _mockPedalEffectTargetOptions = Enum.GetValues<PHprGearPulseTarget>();
    private readonly IReadOnlyList<PHprHidReportTransport> _realPhprReportTransportOptions =
        Enum.GetValues<PHprHidReportTransport>();
    private readonly IReadOnlyList<PhprPedalsModeOption> _phprPedalsModeOptions =
    [
        new(PhprPedalsMode.Disabled, "Disabled"),
        new(PhprPedalsMode.Mock, "Mock"),
        new(PhprPedalsMode.Direct, "Direct")
    ];
    private readonly IReadOnlyList<ReplayTimingModeOption> _replayTimingModeOptions = ReplayTimingModeOption.Defaults;

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
                "The default profile uses the current Stage 18r-B software defaults for this rig.",
                "Audio tuning auto-saves to the default profile under local app data.",
                "Profile values are validated and repaired to the current software ranges on load.",
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
    private bool _startupAsioDefaultsApplied;
    private bool _shutdownCleanupStarted;
    private bool _shutdownCleanupCompleted;
    private AudioOutputDeviceKind _selectedOutputKind = AudioOutputDeviceKind.Null;
    private string? _selectedAsioDriverName;
    private int? _selectedAsioOutputChannel;
    private bool _asioArmed;
    private string? _telemetryStartError;
    private string? _recordingError;
    private string? _replayError;
    private string? _settingsError;
    private bool _hasPersistedOutputModePreference;
    private string _startupProfileStatusMessage = "Current rig defaults loaded.";
    private IReadOnlyList<string> _startupProfileValidationMessages = [];
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
    private bool _updatingBst1PulseUi;
    private bool _advancedDiagnosticsEnabled;
    private bool _routingMockPedalEffects;
    private readonly string _roadTextureFlightRecorderSessionId = Guid.NewGuid().ToString("N");
    private IRoadTextureFlightRecorder _roadTextureFlightRecorder = DisabledRoadTextureFlightRecorder.Instance;
    private DateTimeOffset? _lastPhprCoexistenceScanUtc;
    private string? _lastPhprValidationExportPath;
    private string _lastPhprPedalsPulseMessage = "No normal P-HPR test pulse has been sent.";
    private string _lastBst1PaddleGearPulseMessage = "BST-1 paddle gear pulse is disabled.";
    private bool _bst1PaddleGearPulseEnabled;
    private bool _localGearTestModeEnabled;
    private bool _localGearTestAutoStartListener = true;
    private int _sharedPhprGearPulseDurationMs = Bst1GearPulseDurationSync.DefaultGearDurationMs;
    private float _manualBst1StrengthPercent = 50f;
    private float _bst1OutputTrimPercent = 200f;
    private float _manualBst1FrequencyHz = 50f;
    private int _manualBst1DurationMs = 45;
    private float _bst1PaddleGearStrengthPercent = 50f;
    private float _bst1PaddleGearFrequencyHz = 50f;
    private bool _bst1PaddleGearSyncDuration = true;
    private int _bst1PaddleGearCustomDurationMs = 45;
    private PHprDirectGearPulseRoutingResult? _lastRealPhprGearPulseRoutingResult;
    private List<ForwardingDestinationSetting> _forwardingDestinations = [];
    private List<ForwardingDestinationListItem> _forwardingDestinationItems = [];
    private List<PaddleDeviceListItem> _paddleDeviceItems = [];
    private List<PhprDirectOutputCandidateListItem> _realPhprCandidateItems = [];
    private List<RecordingLibraryItem> _recordingLibraryItems = [];

    public MainWindow()
    {
        _asioVisibilityDiagnostics = new AsioDriverVisibilityDiagnostics(_asioDriverCatalog);
        _asioReadinessDiagnostics = new AsioReadinessDiagnostics(_asioDriverCatalog);

        var appSettings = AppSettingsSnapshotBuilder.BuildHydrationSnapshot(_settingsStore.Load());
        _settingsError = appSettings.SettingsError;
        _lightTheme = appSettings.UseLightTheme;
        _advancedDiagnosticsEnabled = appSettings.AdvancedDiagnosticsEnabled;
        _hasPersistedOutputModePreference = appSettings.HasPersistedOutputModePreference;
        _selectedOutputKind = appSettings.SelectedOutputKind;
        _selectedAsioDriverName = appSettings.SelectedAsioDriverName;
        _selectedAsioOutputChannel = appSettings.SelectedAsioOutputChannel;
        _asioArmed = appSettings.ArmAsioPreference;
        _forwardingDestinations = appSettings.ForwardingDestinations.ToList();
        _paddleMapping = appSettings.PaddleMapping;
        ApplyBst1PaddleGearPulseSetting(appSettings.Bst1PaddleGearPulse);
        _realPhprOptions = appSettings.RealPhprOutputOptions;
        _sharedPhprGearPulseDurationMs = Bst1GearPulseDurationSync.ResolveSharedDuration(
            _realPhprOptions.BrakeGearPulse,
            _realPhprOptions.ThrottleGearPulse);
        _realRoadVibrationOptions = appSettings.RealRoadVibrationRouterOptions;
        _realSlipLockOptions = appSettings.RealSlipLockRouterOptions;
        _shiftIntentProcessor = new ShiftIntentProcessor(
            _drivingArmedStateService,
            appSettings.ShiftIntentOptions);
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
        _realPhprContinuousEffectsRuntime = new PHprContinuousEffectsRuntimeCoordinator(
            _realRoadVibrationRouter,
            _realSlipLockRouter,
            BuildRealContinuousEffectsRuntimeInput);
        _mockGearPulseRouter = new PHprGearPulseRouter(
            _mockPhprSafetyOutput,
            appSettings.MockGearPulseRouterOptions);
        _mockPedalEffectsRouter = new PHprPedalEffectsRouter(
            _mockPhprSafetyOutput,
            appSettings.MockPedalEffectsRouterOptions);
        LoadPersistedAudioProfile();
        _hapticPipeline = CreatePipelineForSelectedOutput();
        _paddleInputRoutingCoordinator = new PaddleInputRoutingCoordinator(
            _shiftIntentProcessor,
            _paddleGearBenchTestController,
            _phprDirectRuntime,
            new PaddleInputRoutingCoordinatorDependencies(
                GetPaddleMapping,
                NotifyAcceptedGearPulse,
                (shiftIntentEvent, cancellationToken) => _mockGearPulseRouter.RouteAsync(
                    shiftIntentEvent,
                    BuildMockGearPulseSafetyContext(shiftIntentEvent),
                    cancellationToken),
                (shiftIntentEvent, cancellationToken) => _realPhprGearPulseRouter.RouteAsync(
                    shiftIntentEvent,
                    BuildRealGearPulseSafetyContext(shiftIntentEvent),
                    cancellationToken),
                (shiftIntentEvent, routeOptions, cancellationToken) => _mockGearPulseRouter.RouteAsync(
                    shiftIntentEvent,
                    routeOptions,
                    BuildPaddleGearBenchMockSafetyContext(),
                    cancellationToken),
                BuildBst1PaddleGearPulseRouteSettings,
                (request, cancellationToken) => _hapticPipeline.StartManualAsioHardwareTestAsync(
                    request,
                    cancellationToken),
                ApplyPhprPedalsNormalOptionsFromControlsAsync,
                ConfigurePhprDirectRuntime,
                _paddleInputSource.GetPaddleSnapshot,
                GetDeviceCardPulseSettings,
                BuildPaddleGearBenchDirectSafetyContext,
                WritePaddleGearBenchCrashLog));

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
        RoadPedalEffectTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        SlipPedalEffectTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        LockPedalEffectTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        RealSlipTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        RealLockTargetComboBox.ItemsSource = _mockPedalEffectTargetOptions;
        RealPhprReportTransportComboBox.ItemsSource = _realPhprReportTransportOptions;
        RealPhprReportTransportComboBox.SelectedItem = PHprHidReportTransport.OutputReport;
        ReplayTimingModeComboBox.ItemsSource = _replayTimingModeOptions;
        ReplayTimingModeComboBox.SelectedItem = ControlSettingsSnapshotBuilder.GetReplayTimingModeOption(appSettings.ReplayTimingPreference);
        ReplayTimingModeHelpText.Text = GetSelectedReplayTimingMode().HelpText;
        ApplyTheme(_lightTheme);
        RefreshForwardingDestinationItems();
        ApplyProfileToControls(_currentProfile);
        ApplyProfileToRuntime(_currentProfile);
        UpdateProfileStatus(_startupProfileStatusMessage, _startupProfileValidationMessages);
        ApplyPaddleMappingToControls();
        ApplyShiftIntentSettingsToControls();
        ApplyMockGearPulseSettingsToControls();
        ApplyMockPedalEffectsSettingsToControls();
        ApplyPhprPedalsNormalSettingsToControls();
        ApplyBst1PulseSettingsToControls();
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
        await ApplyStartupAsioDefaultsAsync();
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
            _realPhprContinuousEffectsRuntime.StartSlipLockRuntime();
            _realPhprContinuousEffectsRuntime.StartRoadVibrationRuntime();
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

    private void LoadPersistedAudioProfile()
    {
        var result = _profileStore.LoadAsync(HapticProfileStore.GetDefaultProfilePath())
            .AsTask()
            .GetAwaiter()
            .GetResult();
        if (result.Succeeded && result.Profile is not null)
        {
            _currentProfile = result.Profile;
            _startupProfileStatusMessage = result.Message;
            _startupProfileValidationMessages = result.ValidationMessages;
            return;
        }

        if (result.Status != HapticProfileLoadStatus.FileNotFound)
        {
            _startupProfileStatusMessage = $"{result.Message} Current rig defaults were used.";
            _startupProfileValidationMessages = result.ValidationMessages;
        }
    }

    private void ApplyBst1PaddleGearPulseSetting(Bst1PaddleGearPulseSetting setting)
    {
        _bst1PaddleGearPulseEnabled = setting.IsEnabled;
        _bst1PaddleGearStrengthPercent = setting.StrengthPercent;
        _bst1PaddleGearFrequencyHz = setting.FrequencyHz;
        _bst1PaddleGearSyncDuration = setting.UseSharedDuration;
        _bst1PaddleGearCustomDurationMs = Bst1GearPulseDurationSync.NormalizeGearDuration(setting.CustomDurationMs);
        _lastBst1PaddleGearPulseMessage = _bst1PaddleGearPulseEnabled
            ? "BST-1 paddle gear pulse enabled for accepted bench Pressed events."
            : "BST-1 paddle gear pulse is disabled.";
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
            TopBarContextText.Text = $"{page.NavigationLabel} / safe control";
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
        SaveAppSettings();
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
        SaveAppSettings();
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
            : "Read-only paddle input listener started. Mapped presses update diagnostics only.";
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

    private async Task RebuildHapticPipelineForOutputSelectionAsync(string footerMessage)
    {
        if (_hapticsStarted || _hapticPipeline.GetSnapshot().IsRunning)
        {
            await _hapticPipeline.StopAsync();
        }

        _hapticsStarted = false;

        var previousPipeline = _hapticPipeline;
        _hapticPipeline = CreatePipelineForSelectedOutput();
        _hapticPipeline.ApplyProfile(_currentProfile);
        await previousPipeline.DisposeAsync();
        var hydrationMessage = await HydrateSelectedOutputReadinessAsync();

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
            configuration,
            CreateSelectedOutputDevice(),
            profile: _currentProfile,
            forwardingDestinations: CreateForwardingDestinations());
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
                ApplyPhprPedalsNormalSettingsToControls();
                ApplyPaddleGearBenchSettingsToControls();
            }

            UpdateRealPhprDirectControlStatus();
            UpdatePhprPedalsStatus();
            UpdatePaddleGearBenchStatus();
            UpdateDiagnosticsStatus();
            FooterStatusText.Text = autoSelection?.HasPreferredCandidate == true
                ? $"Preferred real P-HPR direct candidate selected for startup no-output readiness only; direct control remains disabled until enabled manually. {_realPhprCandidateItems.Count:N0} candidate(s) found. Open-check used no output report or feature report."
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
                    DurationText: RealLockDurationTextBox.Text),
                out var wheelLock,
                out message))
        {
            return false;
        }

        return ControlSettingsSnapshotBuilder.TryBuildRealSlipLockOptions(
            RealSlipLockEnabledCheckBox.IsChecked == true,
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
        RealSlipLockEnabledCheckBox.IsChecked = values.RealSlipLockEnabled;
        RealSlipEnabledCheckBox.IsChecked = values.WheelSlip.IsEnabled;
        RealSlipTargetComboBox.SelectedItem = values.WheelSlip.TargetModule;
        RealSlipMinStrengthTextBox.Text = values.WheelSlip.MinimumStrengthText;
        RealSlipStrengthTextBox.Text = values.WheelSlip.StrengthText;
        RealSlipMinFrequencyTextBox.Text = values.WheelSlip.MinimumFrequencyText;
        RealSlipFrequencyTextBox.Text = values.WheelSlip.FrequencyText;
        RealSlipDurationTextBox.Text = values.WheelSlip.DurationText;
        RealLockEnabledCheckBox.IsChecked = values.WheelLock.IsEnabled;
        RealLockTargetComboBox.SelectedItem = values.WheelLock.TargetModule;
        RealLockMinStrengthTextBox.Text = values.WheelLock.MinimumStrengthText;
        RealLockStrengthTextBox.Text = values.WheelLock.StrengthText;
        RealLockMinFrequencyTextBox.Text = values.WheelLock.MinimumFrequencyText;
        RealLockFrequencyTextBox.Text = values.WheelLock.FrequencyText;
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
            SelectedOutputKind: _selectedOutputKind,
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
        var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
        UpdateHapticsControlState(pipelineSnapshot);
        FooterStatusText.Text = _hapticsStarted
            ? "Haptics started with output-owned low-latency rendering; Null output remains the default unless ASIO was selected, routed, and armed."
            : "Haptics stopped";
        UpdateOutputStatus(result.OutputResult?.Status ?? pipelineSnapshot.Output);
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

        var path = RecordingLibraryManager.FindLatestRecordingPath(GetRecordingsDirectory());
        if (path is null)
        {
            _replayError = "No local .hdrec recording is available to replay yet.";
            FooterStatusText.Text = _replayError;
            UpdateRecordingStatus();
            return;
        }

        _replayError = null;
        var replayMode = GetSelectedReplayTimingMode();
        FooterStatusText.Text = $"Replaying {Path.GetFileName(path)} in {replayMode.Label} mode through the output-owned haptic pipeline.";
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
        var replayMode = GetSelectedReplayTimingMode();
        FooterStatusText.Text = $"Replaying selected recording {Path.GetFileName(item.Path)} in {replayMode.Label} mode.";
        _activeReplayTask = ReplayRecordingAsync(item.Path);
        UpdateRecordingStatus();
    }

    private void ReplayTimingModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var replayMode = GetSelectedReplayTimingMode();
        ReplayTimingModeHelpText.Text = replayMode.HelpText;
        SaveAppSettings();
        UpdateRecordingStatus();
    }

    private async void RefreshRecordingsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRecordingLibraryAsync();
    }

    private async void DeleteSelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            RecordingLibraryStatusText.Text = "Select a recording before deleting.";
            FooterStatusText.Text = RecordingLibraryStatusText.Text;
            return;
        }

        var recordingSnapshot = _hapticPipeline.GetSnapshot().Recording;
        var result = RecordingLibraryManager.DeleteSelected(
            GetRecordingsDirectory(),
            item.Path,
            recordingSnapshot.IsRecording ? recordingSnapshot.FilePath : null);
        await RefreshRecordingLibraryAsync();
        RecordingLibraryStatusText.Text = result.Message;
        FooterStatusText.Text = result.Message;
        UpdateRecordingStatus();
    }

    private async void RenameSelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            RecordingLibraryStatusText.Text = "Select a recording before renaming.";
            FooterStatusText.Text = RecordingLibraryStatusText.Text;
            return;
        }

        var recordingSnapshot = _hapticPipeline.GetSnapshot().Recording;
        var result = RecordingLibraryManager.RenameSelected(
            GetRecordingsDirectory(),
            item.Path,
            RecordingRenameTextBox.Text,
            recordingSnapshot.IsRecording ? recordingSnapshot.FilePath : null);
        await RefreshRecordingLibraryAsync(result.RenamedPath);
        RecordingLibraryStatusText.Text = result.Message;
        FooterStatusText.Text = result.Message;
        UpdateRecordingStatus();
    }

    private void RecordingLibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem item)
        {
            RecordingLibraryStatusText.Text = item.DetailText;
            RecordingRenameTextBox.Text = Path.GetFileNameWithoutExtension(item.Path);
            return;
        }

        RecordingRenameTextBox.Text = string.Empty;
    }

    private async Task ReplayRecordingAsync(string path)
    {
        var replayMode = GetSelectedReplayTimingMode();
        var result = await _hapticPipeline.ReplayFileAsync(path, replayMode.Options);
        await Dispatcher.InvokeAsync(() =>
        {
            _replayError = result.Succeeded ? null : result.Message;
            FooterStatusText.Text = $"{result.Message} Replay mode: {replayMode.Label}.";
            UpdateTelemetryStatus();
            UpdateRecordingStatus();
            UpdateEffectStatus();
            UpdateDiagnosticsStatus();
        });
    }

    private async Task RefreshRecordingLibraryAsync(string? selectedPath = null)
    {
        try
        {
            _recordingLibraryItems = await RecordingLibraryManager.LoadAsync(GetRecordingsDirectory());
            RecordingLibraryListBox.ItemsSource = _recordingLibraryItems;
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                RecordingLibraryListBox.SelectedItem = _recordingLibraryItems.FirstOrDefault(item =>
                    string.Equals(item.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
            }

            RecordingLibraryStatusText.Text = _recordingLibraryItems.Count == 0
                ? $"No .hdrec files found in {GetRecordingsDirectory()}."
                : $"{_recordingLibraryItems.Count} recording(s) found in {GetRecordingsDirectory()}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            RecordingLibraryStatusText.Text = $"Recording library could not be refreshed: {ex.Message}";
        }
    }

    private async void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        _emergencyMuted = !_emergencyMuted;
        var pipelineMuteResult = await _hapticPipeline.SetEmergencyMuteAsync(_emergencyMuted);
        _testBench.EmergencyMute = _emergencyMuted;
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

        FooterStatusText.Text = _emergencyMuted
            ? "Emergency mute is active in the mixer, safety chain, and test bench."
            : "Emergency mute cleared in the mixer, safety chain, and test bench.";
        UpdateEffectStatus();
        UpdateMixerStatus();
        UpdateTestBenchStatus();
        UpdateManualAsioHardwareTestStatus();
    }

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
        HapticsStateText.Text = presentation.HapticsStateText;
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
        ApplyProfileToRuntime(profile);
        var saveResult = PersistCurrentAudioProfile();
        UpdateProfileControlText(profile);
        UpdateMixerStatus();
        UpdateEffectStatus();
        UpdateDiagnosticsStatus();

        if (!saveResult.Succeeded)
        {
            UpdateProfileStatus(saveResult.Message, saveResult.ValidationMessages);
        }

        if (!saveResult.Succeeded)
        {
            FooterStatusText.Text = saveResult.Message;
        }
        else if (_hapticsStarted)
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

    private void ProfileNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingTuningUi)
        {
            return;
        }

        _currentProfile = BuildProfileFromControls();
        var result = PersistCurrentAudioProfile();
        if (!result.Succeeded)
        {
            FooterStatusText.Text = result.Message;
        }

        UpdateProfileStatus(result.Message, result.ValidationMessages);
    }

    private AudioProfileControlInputs BuildCurrentAudioProfileControlInputs()
    {
        return new AudioProfileControlInputs(
            ProfileName: ProfileNameTextBox.Text,
            EngineEnabled: EngineEnabledCheckBox.IsChecked == true,
            EngineGainValue: EngineGainSlider.Value,
            EngineMinimumFrequencyValue: EngineMinimumFrequencySlider.Value,
            EngineMaximumFrequencyValue: EngineMaximumFrequencySlider.Value,
            GearShiftEnabled: GearShiftEnabledCheckBox.IsChecked == true,
            GearShiftGainValue: GearShiftGainSlider.Value,
            GearShiftDurationValue: GearShiftDurationSlider.Value,
            KerbEnabled: KerbEnabledCheckBox.IsChecked == true,
            KerbGainValue: KerbGainSlider.Value,
            KerbBaseFrequencyValue: KerbBaseFrequencySlider.Value,
            ImpactEnabled: ImpactEnabledCheckBox.IsChecked == true,
            ImpactGainValue: ImpactGainSlider.Value,
            ImpactDurationValue: ImpactDurationSlider.Value,
            SharedRoadSignalEnabled: SharedRoadSignalEnabledCheckBox.IsChecked == true,
            Bst1RoadOutputEnabled: Bst1RoadOutputEnabledCheckBox.IsChecked == true,
            RoadTextureGainValue: RoadTextureGainSlider.Value,
            RoadTextureMinimumSpeedValue: RoadTextureMinimumSpeedSlider.Value,
            RoadTextureSpeedReferenceValue: RoadTextureSpeedReferenceSlider.Value,
            RoadTextureLowSpeedFrequencyValue: RoadTextureLowSpeedFrequencySlider.Value,
            RoadTextureHighSpeedFrequencyValue: RoadTextureHighSpeedFrequencySlider.Value,
            RoadTextureSpeedFrequencyInfluenceValue: RoadTextureSpeedFrequencyInfluenceSlider.Value,
            RoadTextureGrainAmountValue: RoadTextureGrainAmountSlider.Value,
            SlipWheelSlipEnabled: SlipWheelSlipEnabledCheckBox.IsChecked == true,
            SlipWheelSlipGainValue: SlipWheelSlipGainSlider.Value,
            SlipWheelSlipFrequencyValue: SlipWheelSlipFrequencySlider.Value,
            SlipWheelSlipNoiseValue: SlipWheelSlipNoiseSlider.Value,
            SlipWheelLockEnabled: SlipWheelLockEnabledCheckBox.IsChecked == true,
            SlipWheelLockGainValue: SlipWheelLockGainSlider.Value,
            SlipWheelLockFrequencyValue: SlipWheelLockFrequencySlider.Value,
            SlipWheelLockNoiseValue: SlipWheelLockNoiseSlider.Value,
            SlipWheelLockSensitivityValue: SlipWheelLockSensitivitySlider.Value,
            SlipThresholdValue: SlipThresholdSlider.Value,
            MasterGainValue: MasterGainSlider.Value,
            MixerMuted: MixerMuteCheckBox.IsChecked == true,
            SafetyOutputGainValue: SafetyOutputGainSlider.Value);
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
    }

    private void ApplyProfileToControls(HapticDriveProfile profile)
    {
        var plan = AudioProfileControlSnapshotBuilder.BuildApplicationPlan(profile);
        var values = plan.ControlValues;
        _updatingTuningUi = true;

        ProfileNameTextBox.Text = values.ProfileName;
        EngineEnabledCheckBox.IsChecked = values.EngineEnabled;
        EngineGainSlider.Value = values.EngineGain;
        EngineMinimumFrequencySlider.Value = values.EngineMinimumFrequencyHz;
        EngineMaximumFrequencySlider.Value = values.EngineMaximumFrequencyHz;
        GearShiftEnabledCheckBox.IsChecked = values.GearShiftEnabled;
        GearShiftGainSlider.Value = values.GearShiftGain;
        GearShiftDurationSlider.Value = values.GearShiftDurationMilliseconds;
        KerbEnabledCheckBox.IsChecked = values.KerbEnabled;
        KerbGainSlider.Value = values.KerbGain;
        KerbBaseFrequencySlider.Value = values.KerbBaseFrequencyHz;
        ImpactEnabledCheckBox.IsChecked = values.ImpactEnabled;
        ImpactGainSlider.Value = values.ImpactGain;
        ImpactDurationSlider.Value = values.ImpactDurationMilliseconds;
        SharedRoadSignalEnabledCheckBox.IsChecked = values.SharedRoadSignalEnabled;
        Bst1RoadOutputEnabledCheckBox.IsChecked = values.Bst1RoadOutputEnabled;
        RoadTextureGainSlider.Value = values.RoadTextureGain;
        RoadTextureMinimumSpeedSlider.Value = values.RoadTextureMinimumSpeedKph;
        RoadTextureSpeedReferenceSlider.Value = values.RoadTextureSpeedReferenceKph;
        RoadTextureLowSpeedFrequencySlider.Value = values.RoadTextureLowSpeedFrequencyHz;
        RoadTextureHighSpeedFrequencySlider.Value = values.RoadTextureHighSpeedFrequencyHz;
        RoadTextureSpeedFrequencyInfluenceSlider.Value = values.RoadTextureSpeedFrequencyInfluence;
        RoadTextureGrainAmountSlider.Value = values.RoadTextureGrainAmount;
        SlipWheelSlipEnabledCheckBox.IsChecked = values.SlipWheelSlipEnabled;
        SlipWheelSlipGainSlider.Value = values.SlipWheelSlipGain;
        SlipWheelSlipFrequencySlider.Value = values.SlipWheelSlipFrequencyHz;
        SlipWheelSlipNoiseSlider.Value = values.SlipWheelSlipNoiseAmount;
        SlipWheelLockEnabledCheckBox.IsChecked = values.SlipWheelLockEnabled;
        SlipWheelLockGainSlider.Value = values.SlipWheelLockGain;
        SlipWheelLockFrequencySlider.Value = values.SlipWheelLockFrequencyHz;
        SlipWheelLockNoiseSlider.Value = values.SlipWheelLockNoiseAmount;
        SlipWheelLockSensitivitySlider.Value = values.SlipWheelLockSensitivity;
        SlipThresholdSlider.Value = values.SlipThreshold;
        MasterGainSlider.Value = values.MasterGain;
        MixerMuteCheckBox.IsChecked = values.MixerMuted;
        SafetyOutputGainSlider.Value = values.SafetyOutputGain;

        _updatingTuningUi = false;
        ApplyProfileControlText(plan.TextValues);
    }

    private void UpdateProfileControlText(HapticDriveProfile profile)
    {
        ApplyProfileControlText(AudioProfileControlSnapshotBuilder.BuildApplicationPlan(profile).TextValues);
    }

    private void ApplyProfileControlText(AudioProfileControlTextValues values)
    {
        EngineGainValueText.Text = values.EngineGainText;
        EngineFrequencyValueText.Text = values.EngineFrequencyText;
        GearShiftGainValueText.Text = values.GearShiftGainText;
        GearShiftDurationValueText.Text = values.GearShiftDurationText;
        KerbGainValueText.Text = values.KerbGainText;
        KerbFrequencyValueText.Text = values.KerbFrequencyText;
        ImpactGainValueText.Text = values.ImpactGainText;
        ImpactDurationValueText.Text = values.ImpactDurationText;
        RoadTextureGainValueText.Text = values.RoadTextureGainText;
        RoadTextureMinimumSpeedValueText.Text = values.RoadTextureMinimumSpeedText;
        RoadTextureSpeedReferenceValueText.Text = values.RoadTextureSpeedReferenceText;
        RoadTextureLowSpeedFrequencyValueText.Text = values.RoadTextureLowSpeedFrequencyText;
        RoadTextureHighSpeedFrequencyValueText.Text = values.RoadTextureHighSpeedFrequencyText;
        RoadTextureSpeedFrequencyInfluenceValueText.Text = values.RoadTextureSpeedFrequencyInfluenceText;
        RoadTextureGrainAmountValueText.Text = values.RoadTextureGrainAmountText;
        SlipWheelSlipGainValueText.Text = values.SlipWheelSlipGainText;
        SlipWheelSlipFrequencyValueText.Text = values.SlipWheelSlipFrequencyText;
        SlipWheelSlipNoiseValueText.Text = values.SlipWheelSlipNoiseText;
        SlipWheelLockGainValueText.Text = values.SlipWheelLockGainText;
        SlipWheelLockFrequencyValueText.Text = values.SlipWheelLockFrequencyText;
        SlipWheelLockNoiseValueText.Text = values.SlipWheelLockNoiseText;
        SlipWheelLockSensitivityValueText.Text = values.SlipWheelLockSensitivityText;
        SlipThresholdValueText.Text = values.SlipThresholdText;
        MasterGainValueText.Text = values.MasterGainText;
        SafetyOutputGainValueText.Text = values.SafetyOutputGainText;
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
        ApplyBst1PaddleGearPulseSetting(new Bst1PaddleGearPulseSetting());
        ApplyBst1PulseSettingsToControls();
        var audioSaveResult = PersistCurrentAudioProfile();
        SaveAppSettings();
        UpdateEffectStatus();
        UpdateMixerStatus();
        UpdateProfileStatus(
            audioSaveResult.Succeeded
                ? "Reset to current rig audio, BST-1 local gear, and P-HPR defaults."
                : audioSaveResult.Message,
            audioSaveResult.ValidationMessages);

        if (!audioSaveResult.Succeeded)
        {
            FooterStatusText.Text = audioSaveResult.Message;
            return;
        }

        if (_hapticsStarted)
        {
            FooterStatusText.Text = "Reset tuning to the current rig defaults for the output-owned render path.";
            return;
        }

        FooterStatusText.Text = "Reset tuning to the current rig defaults.";
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
            await RebuildHapticPipelineForOutputSelectionAsync(selection.Message);
            return;
        }

        UpdateManualAsioHardwareTestStatus();
        FooterStatusText.Text = selection.Message;
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
        TrueAsioStatusText.Text = BuildTrueAsioStatusText(snapshot);
        ManualAsioHardwareStatusText.Text =
            $"BST-1 channel {(snapshot.SelectedOutputChannel is null ? "none" : snapshot.SelectedOutputChannel)}; driver {snapshot.SelectedAsioDriver}; armed {snapshot.AsioArmed}; haptics {(snapshot.HapticsRunning ? "running" : "stopped")}; peak {snapshot.ManualPulsePeak:0.000}.";
        ManualAsioHardwareBlockedReasonText.Text = snapshot.BlockedReason is null
            ? $"{Bst1AsioStatusFormatter.FormatLastPulseCompact(snapshot)}; {_lastBst1PaddleGearPulseMessage}"
            : Bst1AsioStatusFormatter.FormatLastPulseCompact(snapshot);
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
        OutputModeValueText.Text = status.DisplayName;
        OutputModeDetailText.Text = status.Kind == AudioOutputDeviceKind.Asio
            ? $"{status.StatusMessage} Armed {status.IsHardwareArmed}; channel {(status.SelectedOutputChannel is null ? "not selected" : status.SelectedOutputChannel)}."
            : status.StatusMessage;
        UpdateDeviceStatus();
    }

    private void UpdateMixerStatus()
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
        MasterGainValueText.Text = $"{mixer.MasterGain:P0}";
        SafetyOutputGainValueText.Text = $"{safety.OutputGain:P0}";
        MixerEmergencyMuteStatusText.Text = $"Emergency mute: {FormatOnOff(_emergencyMuted)}; normal mute: {FormatOnOff(pipelineSnapshot.IsMuted)}.";
        MixerOutputPeakStatusText.Text = $"Output peak: {audioDiagnostics.OutputPeakLevel:0.000}; mixer peak {audioDiagnostics.MixerPeakLevel:0.000}.";
        MixerLimiterActivityStatusText.Text =
            $"Limiter: always on; internal ceiling {safety.OutputGainCeiling:P0}; limited samples {audioDiagnostics.LimitedSampleCount:N0}; clipped samples {audioDiagnostics.ClippedSampleCount:N0}.";

        var outputStatus = pipelineSnapshot.Output;
        var manualAsio = _hapticPipeline.GetManualAsioHardwareTestSnapshot();
        var selectedOutputMode = OutputModeComboBox.SelectedItem is OutputModeOption selectedMode
            ? selectedMode.Label
            : outputStatus.DisplayName;
        Bst1RoutingSummaryText.Text =
            $"Output mode {selectedOutputMode}; selected driver {_selectedAsioDriverName ?? "none"}; channel {(_selectedAsioOutputChannel is null ? "none" : _selectedAsioOutputChannel)}; armed {_asioArmed}; readiness {BuildTrueAsioStatusText(manualAsio)}.";
        Bst1EffectsSummaryText.Text =
            $"Effects: gear {FormatEnabledActive(_bst1PaddleGearPulseEnabled, effectSnapshot.GearShift.IsActive)}; road {FormatEnabledActive(effectSnapshot.RoadTexture.Bst1OutputEnabled, effectSnapshot.RoadTexture.IsActive)}; engine {FormatEnabledActive(effectSnapshot.Engine.IsEnabled, effectSnapshot.Engine.IsActive)}; kerb {FormatEnabledActive(effectSnapshot.Kerb.IsEnabled, effectSnapshot.Kerb.IsActive)}; impact {FormatEnabledActive(effectSnapshot.Impact.IsEnabled, effectSnapshot.Impact.IsActive)}; slip {FormatEnabledActive(effectSnapshot.Slip.WheelSlipEnabled, effectSnapshot.Slip.IsActive && string.Equals(effectSnapshot.Slip.ActiveSource, "Wheel slip", StringComparison.Ordinal))}; lock {FormatEnabledActive(effectSnapshot.Slip.WheelLockEnabled, effectSnapshot.Slip.IsActive && string.Equals(effectSnapshot.Slip.ActiveSource, "Wheel lock", StringComparison.Ordinal))}.";

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
        BrakePhprRoutingSummaryText.Text =
            $"Mode {PhprPedalsModeComboBox.SelectedItem}; P-HPR {FormatEnabled(_realPhprOptions.DirectControlEnabled)}; {directReadiness}; connection {realOutput.Connection.State}; writes {realOutput.ReportWriteCount:N0}.";
        BrakePhprEffectsSummaryText.Text =
            $"Effects: gear {FormatEnabledActive(_realPhprOptions.BrakeGearPulse.IsEnabled, directRuntime.HardwareBelievedActive)}; road {FormatEnabledActive(_realRoadVibrationOptions.IsEnabled && _realRoadVibrationOptions.Brake.IsEnabled, continuousRuntime.LastRoadVibrationRoutingResult?.WasRouted == true)}; lock {FormatEnabledActive(_realSlipLockOptions.IsEnabled && _realSlipLockOptions.WheelLock.IsEnabled, brakeSlipLockActive)}.";
        ThrottlePhprRoutingSummaryText.Text =
            $"Mode {PhprPedalsModeComboBox.SelectedItem}; P-HPR {FormatEnabled(_realPhprOptions.DirectControlEnabled)}; {directReadiness}; coexistence {_phprSoftwareCoexistenceSnapshot.Status}; emergency stop {FormatOnOff(realOutput.Output.IsEmergencyStopActive)}.";
        ThrottlePhprEffectsSummaryText.Text =
            $"Effects: gear {FormatEnabledActive(_realPhprOptions.ThrottleGearPulse.IsEnabled, directRuntime.HardwareBelievedActive)}; road {FormatEnabledActive(_realRoadVibrationOptions.IsEnabled && _realRoadVibrationOptions.Throttle.IsEnabled, continuousRuntime.LastRoadVibrationRoutingResult?.WasRouted == true)}; slip {FormatEnabledActive(_realSlipLockOptions.IsEnabled && _realSlipLockOptions.WheelSlip.IsEnabled, throttleSlipLockActive)}.";

        ActiveEffectsSummaryText.Text =
            $"{effectSnapshot.ActiveEffectCount:N0} active source(s); engine {FormatActiveIdle(effectSnapshot.Engine.IsActive)}; gear {FormatActiveIdle(effectSnapshot.GearShift.IsActive)}; road {FormatActiveIdle(effectSnapshot.RoadTexture.IsActive)}; kerb {FormatActiveIdle(effectSnapshot.Kerb.IsActive)}; impact {FormatActiveIdle(effectSnapshot.Impact.IsActive)}; slip/lock {FormatActiveIdle(effectSnapshot.Slip.IsActive)}; output peak {audioDiagnostics.OutputPeakLevel:0.000}.";
        PriorityDuckingSummaryText.Text =
            "Emergency Stop and Emergency Mute override all output. Gear pulses remain highest priority on P-HPR. Continuous P-HPR slip/lock now routes above P-HPR road, road yields while slip/lock is active, and telemetry-stale slip/lock or road output is suppressed.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Routing / Mixer" })
        {
            PageStatusText.Text = $"Master {mixer.MasterGain:P0}; mute {FormatOnOff(pipelineSnapshot.IsMuted)}; emergency mute {FormatOnOff(_emergencyMuted)}; output peak {audioDiagnostics.OutputPeakLevel:0.000}; active effects {effectSnapshot.ActiveEffectCount:N0}.";
        }
    }

    private void UpdateDeviceStatus()
    {
        var snapshot = _hapticPipeline.GetSnapshot();
        var status = snapshot.Output;
        CurrentOutputStatusText.Text = $"Current output: {status.DisplayName} ({status.State}); {status.StatusMessage}";
        NullOutputStatusText.Text = "Null output: default automated-test and hardware-absent target; produces no physical sound.";
        WasapiDebugStatusText.Text = "WASAPI debug: manual placeholder only; it is not the ASIO target and is never selected automatically.";
        AsioStatusText.Text = _asioVisibilitySnapshot.Message;
        AsioReadinessStatusText.Text = $"{BuildTrueAsioStatusText(_hapticPipeline.GetManualAsioHardwareTestSnapshot())}; driver {(_selectedAsioDriverName ?? "none")}; channel {(_selectedAsioOutputChannel is null ? "none" : _selectedAsioOutputChannel)}; armed {_asioArmed}.";
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
        UpdateLocalGearTestStatus();
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
        var settingsPresentation = BuildPersistedSettingsStatusPresentation();
        ProfileStatusText.Text = message ?? $"Active profile: {_currentProfile.Name}.";
        ProfilePathText.Text = $"Audio profile path: {path}; normal BST-1/audio tuning auto-saves here for the next launch.";
        ProfilePhprStatusText.Text = $"P-HPR profile path: {phprPath}; manual save/load snapshot for shift intent, mock gear/pedal effects, and real gear/road/slip/lock preferences. Startup restore uses app settings for safe P-HPR preferences; direct enable/arm/device and paddle bench state remain runtime-only.";
        ProfileValidationText.Text = validationMessages is { Count: > 0 }
            ? string.Join(" ", validationMessages)
            : "Profile values are clamped to the current Stage 18r-C software ranges on load and save.";
        SettingsStatusText.Text = settingsPresentation.StatusText;
        SettingsPathText.Text = settingsPresentation.PathText;
        RuntimePrerequisiteText.Text = $".NET Desktop runtime is available for this running WPF app. Launch script sets DOTNET_ROOT to the repo-local .NET 8 runtime before starting the executable.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Profiles" })
        {
            PageStatusText.Text = $"Active profile {_currentProfile.Name}; audio JSON version {HapticDriveProfile.CurrentVersion}; P-HPR JSON version {PhprEffectProfile.CurrentVersion}; startup restores saved tuning without auto-starting haptics or direct output.";
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Advanced / Diagnostics" })
        {
            PageStatusText.Text = $"Theme {(_lightTheme ? "light" : "dark")}; app settings are local; ASIO ready/arm preference can persist without starting output, while haptics running and direct-output enable/arm/device runtime states remain non-persistent.";
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
            SelectedOutputKind: _selectedOutputKind,
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
        var realDiagnostics = _realPhprOutput.GetDiagnostics();
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var presentation = BuildPhprWorkflowStatusPresentation(pipelineSnapshot, realDiagnostics);

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
            $"Slip/lock: {(_realSlipLockOptions.IsEnabled ? "enabled" : "disabled")}; slip {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelSlip, _realSlipLockOptions.WheelSlip)}; lock {FormatRealSlipLockEffect(PHprPedalEffectKind.WheelLock, _realSlipLockOptions.WheelLock)}; details {BuildRealSlipLockDiagnosticsText()}.",
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
            PipelineText: $"{(pipelineSnapshot.IsRunning ? "running" : "stopped")}; source {pipelineSnapshot.InputSource}; rendered {pipelineSnapshot.RenderedBufferCount:N0} buffer(s); telemetry age {(pipelineSnapshot.TelemetryAge is null ? "none" : $"{pipelineSnapshot.TelemetryAge.Value.TotalMilliseconds:0} ms")}; stale mute {pipelineSnapshot.TelemetryTimedOutMuted}; last error {pipelineSnapshot.LastPipelineError ?? "none"}.",
            UdpListenerText: $"{(receiverSnapshot.IsRunning ? "running" : "stopped")} on port {receiverSnapshot.BoundPort}; rate {receiverSnapshot.PacketRatePerSecond:0.00}/s; last packet {(receiverSnapshot.LastPacketAtUtc is null ? "never" : $"{receiverSnapshot.TimeSinceLastPacket?.TotalSeconds:0.0}s ago")}.",
            UdpForwardingText: $"{forwarderSnapshot.EnabledDestinationCount}/{forwarderSnapshot.DestinationCount} destination(s) enabled; {forwarderSnapshot.ForwardedDatagramCount:N0} datagrams; {forwarderSnapshot.ErrorCount:N0} error(s).",
            UdpForwardingDestinationsText: BuildForwardingDestinationsText(),
            ParserText: $"{parserSuccess:N0} valid, {parserIgnored:N0} ignored, {parserFailed:N0} failed. {pipelineSnapshot.LastPacketMessage}",
            PacketIdsText: packetDiagnostics,
            VehicleStateText: $"{vehicleUpdates:N0} update(s). {pipelineSnapshot.LastVehicleStateMessage}",
            RecordingText: $"{(recordingSnapshot.IsRecording ? "active" : "inactive")}; {recordingSnapshot.PacketCount:N0} packet(s); file {(recordingSnapshot.FilePath is null ? "none" : Path.GetFileName(recordingSnapshot.FilePath))}.",
            ReplayText: $"{(replaySnapshot.IsReplaying ? "active" : "inactive")}; source {FormatReplaySource(pipelineSnapshot)}; {replaySnapshot.PacketsReplayed:N0} packet(s); {replaySnapshot.StatusMessage}",
            EffectsText: $"enabled engine {effectSnapshot.Engine.IsEnabled}, gear {effectSnapshot.GearShift.IsEnabled}, kerb {effectSnapshot.Kerb.IsEnabled}, impact {effectSnapshot.Impact.IsEnabled}, road {effectSnapshot.RoadTexture.IsEnabled}, slip {effectSnapshot.Slip.WheelSlipEnabled}, lock {effectSnapshot.Slip.WheelLockEnabled}; overall slip/lock {effectSnapshot.Slip.IsEnabled}; peak {effectSnapshot.PeakLevel:0.000}.",
            Bst1SlipLockText: $"source {effectSnapshot.Slip.ActiveSource}; reason {effectSnapshot.Slip.ActiveReason}; slip intensity {effectSnapshot.Slip.CurrentSlipIntensity:0.00}; lock intensity {effectSnapshot.Slip.CurrentLockIntensity:0.00}; slip ratio {effectSnapshot.Slip.CurrentSlipRatio:0.00}; slip angle {effectSnapshot.Slip.CurrentSlipAngleRadians:0.00} rad; wheel-speed ratio {effectSnapshot.Slip.CurrentMinimumWheelSpeedRatio:0.00}; frequency {effectSnapshot.Slip.CurrentFrequencyHz:0.0} Hz; roughness {effectSnapshot.Slip.CurrentNoiseAmount:P0}; peak {effectSnapshot.Slip.PeakLevel:0.000}.",
            MixerSafetyText: $"mixer peak {audioDiagnostics.MixerPeakLevel:0.000}; output peak {audioDiagnostics.OutputPeakLevel:0.000}; limited {audioDiagnostics.LimitedSampleCount:N0}; clipped {audioDiagnostics.ClippedSampleCount:N0}; emergency mute {audioDiagnostics.EmergencyMute}.",
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

    private void ApplyDiagnosticsStatusPresentation(DiagnosticsStatusPresentation presentation)
    {
        RoadTextureFlightRecorderStatusText.Text = presentation.RoadRecorderStatusText;
        DiagnosticsSummaryText.Text = presentation.SummaryText;
        DiagnosticsItemsControl.ItemsSource = presentation.Items;

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
        return $"{lastResult?.Status.ToString() ?? "none"}; runtime {snapshot.RuntimeState}; active {snapshot.ActiveSlipLockModules}; cadence {snapshot.Options.MinimumRouteInterval.TotalMilliseconds:0} ms; hold {snapshot.Options.HoldTimeout.TotalMilliseconds:0} ms; routed {snapshot.RouteCount:N0}; safety rejected {snapshot.SafetyRejectedCount:N0}; interval suppressed {snapshot.IntervalSuppressedCount:N0}; stale {snapshot.StaleTelemetrySuppressedCount:N0}; command-rate {snapshot.CommandRateSuppressedCount:N0}; stops {snapshot.StopCommandCount:N0}; gear protected {snapshot.GearProtectionSuppressedCount:N0}; road yield {continuousRuntime.RoadHigherPrioritySuppressedCount:N0}; last target {snapshot.LastTargetModule?.ToString() ?? "none"}; slip {FormatRealSlipLockEffectDiagnostics(snapshot.WheelSlip, includeTelemetry: false)}; lock {FormatRealSlipLockEffectDiagnostics(snapshot.WheelLock, includeTelemetry: false)}; at {FormatTimestamp(lastResult?.RoutedAtUtc ?? snapshot.LastRouteAttemptAtUtc)}";
    }

    private string BuildRealSlipLockDiagnosticsText()
    {
        var continuousRuntime = _realPhprContinuousEffectsRuntime.GetSnapshot();
        var snapshot = _realSlipLockRouter.GetSnapshot();
        if (snapshot.RouteAttemptCount <= 0 && snapshot.LastResult is null)
        {
            return "none";
        }

        return $"runtime {snapshot.RuntimeState}; active {snapshot.ActiveSlipLockModules}; cadence {snapshot.Options.MinimumRouteInterval.TotalMilliseconds:0} ms; hold {snapshot.Options.HoldTimeout.TotalMilliseconds:0} ms; attempts {snapshot.RouteAttemptCount:N0}; routed {snapshot.RouteCount:N0}; safety rejected {snapshot.SafetyRejectedCount:N0}; interval suppressed {snapshot.IntervalSuppressedCount:N0}; stale {snapshot.StaleTelemetrySuppressedCount:N0}; command-rate {snapshot.CommandRateSuppressedCount:N0}; stops {snapshot.StopCommandCount:N0}; gear protected {snapshot.GearProtectionSuppressedCount:N0}; road yield {continuousRuntime.RoadHigherPrioritySuppressedCount:N0}; watchdog stops {snapshot.WatchdogStopCount:N0}; stop reason {snapshot.LastSlipLockStopReason}; last result {snapshot.LastResult?.Status.ToString() ?? "none"}; last target {snapshot.LastTargetModule?.ToString() ?? "none"}; start age {FormatTimestampAge(snapshot.LastSlipLockStartAtUtc)}; update age {FormatTimestampAge(snapshot.LastSlipLockUpdateAtUtc)}; stop age {FormatTimestampAge(snapshot.LastSlipLockStopAtUtc)}; slip {FormatRealSlipLockEffectDiagnostics(snapshot.WheelSlip, includeTelemetry: true)}; lock {FormatRealSlipLockEffectDiagnostics(snapshot.WheelLock, includeTelemetry: true)}";
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
        return $"{(normalized.IsEnabled ? "on" : "off")} target {normalized.TargetModule}; strength {PhprUiValueConverter.FormatPercent(normalized.MinimumStrength01)}-{PhprUiValueConverter.FormatPercent(normalized.Strength01)}%; freq {PhprUiValueConverter.FormatFrequency(normalized.MinimumFrequencyHz)}-{PhprUiValueConverter.FormatFrequency(normalized.FrequencyHz)} Hz; duration {normalized.DurationMs} ms";
    }

    private static string FormatRealSlipLockEffectDiagnostics(
        PHprSlipLockEffectRoutingDiagnostics diagnostics,
        bool includeTelemetry)
    {
        var target = diagnostics.LastTargetModule?.ToString() ?? diagnostics.Settings.TargetModule.ToString();
        var telemetry = includeTelemetry
            ? $"; raw {FormatRealSlipLockTelemetry(diagnostics.Kind, diagnostics.LastTelemetry)}"
            : string.Empty;
        return $"{diagnostics.Kind}: active {diagnostics.LastActive}; reason {diagnostics.LastReason}; target {target}; intensity {diagnostics.LastIntensity01:0.###}; strength {PhprUiValueConverter.FormatPercent(diagnostics.LastComputedStrength01)}%; freq {PhprUiValueConverter.FormatFrequency(diagnostics.LastComputedFrequencyHz)} Hz; duration {diagnostics.LastCommandDurationMs:N0} ms; below tactile {diagnostics.LastBelowTactileThreshold}; routed {diagnostics.RouteCount:N0}; safety rejected {diagnostics.SafetyRejectedCount:N0}; interval suppressed {diagnostics.IntervalSuppressedCount:N0}; stale {diagnostics.StaleTelemetrySuppressedCount:N0}; command-rate {diagnostics.CommandRateSuppressedCount:N0}; stops {diagnostics.StopCommandCount:N0}; start age {FormatTimestampAge(diagnostics.LastStartAtUtc)}; update age {FormatTimestampAge(diagnostics.LastUpdateAtUtc)}; stop age {FormatTimestampAge(diagnostics.LastStopAtUtc)}{telemetry}";
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

        await RebuildHapticPipelineForOutputSelectionAsync(plan.Message);
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
        RecordRoadTextureFlightRecorder(pipelineSnapshot);
        UpdateTelemetryStatus();
        UpdateHapticsControlState(pipelineSnapshot);
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

    private bool IsRealPhprPedalRoutingReady(HapticPipelineSnapshot pipelineSnapshot)
    {
        return _realPhprOptions.DirectControlEnabled
            && !_realPhprOptions.CandidateIsRawInputOnly
            && _realPhprOptions.CandidateHasOpenableHidPath
            && _realPhprOptions.OpenCheckSucceeded
            && _realPhprOptions.AllowsDirectPulseReportShape
            && _realPhprOptions.Selector.IsSelected
            && pipelineSnapshot.VehicleStateUpdateCount > 0;
    }

    private PHprContinuousEffectsRuntimeInput BuildRealContinuousEffectsRuntimeInput()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        return new PHprContinuousEffectsRuntimeInput(
            pipelineSnapshot,
            IsRealPhprPedalRoutingReady(pipelineSnapshot),
            BuildRealRoadVibrationSafetyContext(pipelineSnapshot),
            BuildRealSlipLockSafetyContext(pipelineSnapshot));
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

    private PHprSafetyContext BuildRealRoadVibrationSafetyContext(HapticPipelineSnapshot pipelineSnapshot)
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
                outputSnapshot,
                pipelineSnapshot.TelemetryTimedOutMuted,
                hapticsStopped: !pipelineSnapshot.IsRunning,
                _emergencyMuted,
                driving.Current.IsArmed,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
    }

    private PHprSafetyContext BuildRealSlipLockSafetyContext(HapticPipelineSnapshot pipelineSnapshot)
    {
        var outputSnapshot = _realPhprOutput.GetSnapshot();
        var driving = _drivingArmedStateService.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
                outputSnapshot,
                pipelineSnapshot.TelemetryTimedOutMuted,
                hapticsStopped: !pipelineSnapshot.IsRunning,
                _emergencyMuted,
                driving.Current.IsArmed,
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
        HapticPipelineSnapshot pipelineSnapshot,
        bool drivingArmed)
    {
        var outputSnapshot = _mockPhprSafetyOutput.GetSnapshot();
        return SafetyContextSnapshotBuilder.BuildMockRuntimeSnapshot(
                outputSnapshot,
                pipelineSnapshot.TelemetryTimedOutMuted,
                hapticsStopped: !pipelineSnapshot.IsRunning,
                _emergencyMuted,
                drivingArmed,
                _phprSoftwareCoexistenceSnapshot.Status)
            .ToSafetyContext();
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

        var sharedRoadSignalActive = snapshot.RoadTexture.Signal.IsActive;
        SharedRoadSignalStatusText.Text = $"Shared road signal {(options.RoadTexture.IsEnabled ? "enabled" : "disabled")}; output {snapshot.RoadTexture.Signal.OutputIntensity:0.000}; speed scale {snapshot.RoadTexture.Signal.SpeedScale:0.000}; gear ducking {snapshot.RoadTexture.Signal.GearDuckingActive}.";
        RoadTextureEffectStateText.Text = snapshot.RoadTexture.IsActive ? "BST-1 active" : "Idle";
        RoadTextureEffectDetailText.Text = snapshot.RoadTexture.DominantSurfaceTypeId is null
            ? "Waiting for speed and surface telemetry."
            : $"{snapshot.RoadTexture.DominantSurfaceName}; shared signal {(sharedRoadSignalActive ? "active" : "idle")}; mix {snapshot.RoadTexture.SurfaceMix:0.00}; speed {snapshot.RoadTexture.Signal.SpeedKph} km/h; {snapshot.RoadTexture.CurrentFrequencyHz:0.0} Hz; grain {snapshot.RoadTexture.Signal.NoiseAmount:P0}; BST-1 peak {snapshot.RoadTexture.PeakLevel:0.000}.";
        RoadTextureEffectDefaultsText.Text = $"BST-1 / ASIO road output gain {options.RoadTexture.Gain:P0}; min {options.RoadTexture.MinimumSpeedKph:0} km/h; speed ref {options.RoadTexture.FullIntensitySpeedKph:0} km/h; {options.RoadTexture.Bst1LowSpeedFrequencyHz:0}-{options.RoadTexture.Bst1HighSpeedFrequencyHz:0} Hz; speed influence {options.RoadTexture.Bst1SpeedFrequencyInfluence:P0}; grain {options.RoadTexture.Bst1GrainAmount:P0}; shared signal {options.RoadTexture.IsEnabled}; BST-1 output {options.RoadTexture.Bst1OutputEnabled}.";

        SlipEffectStateText.Text = snapshot.Slip.IsActive ? $"{snapshot.Slip.ActiveSource} active" : "Idle";
        SlipEffectDetailText.Text = snapshot.Slip.CurrentSlipRatio <= 0f
                                     && snapshot.Slip.CurrentSlipAngleRadians <= 0f
                                     && Math.Abs(snapshot.Slip.CurrentMinimumWheelSpeedRatio - 1f) < 0.0001f
            ? "Waiting for Motion Ex slip ratio / angle and wheel-speed telemetry."
            : $"{snapshot.Slip.ActiveReason}; slip {snapshot.Slip.CurrentSlipIntensity:0.00} (ratio {snapshot.Slip.CurrentSlipRatio:0.00}, angle {snapshot.Slip.CurrentSlipAngleRadians:0.00} rad); lock {snapshot.Slip.CurrentLockIntensity:0.00} (wheel-speed ratio {snapshot.Slip.CurrentMinimumWheelSpeedRatio:0.00}); {snapshot.Slip.CurrentFrequencyHz:0.0} Hz; roughness {snapshot.Slip.CurrentNoiseAmount:P0}; peak {snapshot.Slip.PeakLevel:0.000}.";
        SlipEffectDefaultsText.Text = $"Slip {options.Slip.WheelSlipGain:P0} @ {options.Slip.WheelSlipFrequencyHz:0} Hz, roughness {options.Slip.WheelSlipNoiseAmount:P0}, threshold {options.Slip.SlipRatioThreshold:0.00}, enabled {options.Slip.WheelSlipEnabled}; lock {options.Slip.WheelLockGain:P0} @ {options.Slip.WheelLockFrequencyHz:0} Hz, roughness {options.Slip.WheelLockNoiseAmount:P0}, sensitivity {options.Slip.BrakeLockWheelSpeedRatioThreshold:0.00}, enabled {options.Slip.WheelLockEnabled}.";

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
        var replayMode = GetSelectedReplayTimingMode();
        if (_replayError is not null)
        {
            return $"{_replayError} Replay mode: {replayMode.Label}.";
        }

        return snapshot.IsReplaying
            ? $"Replay active from {Path.GetFileName(snapshot.SourceFilePath)}; mode {replayMode.Label}; {snapshot.PacketsReplayed:N0} packet(s)."
            : $"Replay inactive; mode {replayMode.Label}; {snapshot.PacketsReplayed:N0} packet(s) last replayed. {snapshot.StatusMessage}";
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

    private static string CreateDefaultRecordingPath()
    {
        var recordingsDirectory = GetRecordingsDirectory();
        Directory.CreateDirectory(recordingsDirectory);

        return Path.Combine(
            recordingsDirectory,
            $"f1-25-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.hdrec");
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

    protected override void OnClosing(CancelEventArgs e)
    {
        var minimizeToTrayEnabled = IsMinimizeToTrayOnCloseEnabled();
        if (minimizeToTrayEnabled)
        {
            e.Cancel = true;
            return;
        }

        if (!_shutdownCleanupCompleted && !_shutdownCleanupStarted)
        {
            _shutdownCleanupStarted = true;
            IsEnabled = false;
            FooterStatusText.Text = "Shutting down ASIO and listener resources...";
            RunShutdownCleanupBlocking();
            _shutdownCleanupCompleted = true;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private async Task ShutdownThenCloseAsync()
    {
        try
        {
            await RunShutdownCleanupAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            RecordShutdownDiagnostic(
                "shutdown-cleanup-failed",
                DateTimeOffset.UtcNow,
                minimizeToTrayEnabled: false,
                asioDisposed: false,
                standalonePulseDisposed: false,
                paddleListenerDisposed: false,
                udpListenerDisposed: false,
                timersDisposed: false,
                pendingTaskCount: _activeReplayTask is { IsCompleted: false } ? 1 : 0,
                [$"shutdown:{ex.GetType().Name}:{ex.Message}"]);
        }
        finally
        {
            _shutdownCleanupCompleted = true;
            Close();
        }
    }

    private void RunShutdownCleanupBlocking()
    {
        try
        {
            RunShutdownCleanupAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            RecordShutdownDiagnostic(
                "shutdown-cleanup-failed",
                DateTimeOffset.UtcNow,
                minimizeToTrayEnabled: false,
                asioDisposed: false,
                standalonePulseDisposed: false,
                paddleListenerDisposed: false,
                udpListenerDisposed: false,
                timersDisposed: false,
                pendingTaskCount: _activeReplayTask is { IsCompleted: false } ? 1 : 0,
                [$"shutdown:{ex.GetType().Name}:{ex.Message}"]);
        }
    }

    private static bool IsMinimizeToTrayOnCloseEnabled()
    {
        return false;
    }

    private async Task RunShutdownCleanupAsync()
    {
        var shutdownStartedAtUtc = DateTimeOffset.UtcNow;
        var minimizeToTrayEnabled = false;
        var asioDisposed = false;
        var standalonePulseDisposed = false;
        var paddleListenerDisposed = false;
        var udpListenerDisposed = false;
        var timersDisposed = false;
        var shutdownExceptions = new List<string>();

        RecordShutdownDiagnostic(
            "shutdown-requested",
            shutdownStartedAtUtc,
            minimizeToTrayEnabled,
            asioDisposed,
            standalonePulseDisposed,
            paddleListenerDisposed,
            udpListenerDisposed,
            timersDisposed,
            pendingTaskCount: _activeReplayTask is { IsCompleted: false } ? 1 : 0,
            shutdownExceptions);

        var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();
        foreach (var step in plan.Steps)
        {
            switch (step.Kind)
            {
                case ShutdownCleanupStepKind.DetachUnhandledExceptionAndInputTelemetryHandlers:
                    Dispatcher.UnhandledException -= MainWindow_DispatcherUnhandledException;
                    AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                    TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
                    _paddleInputSource.RawButtonChanged -= PaddleInputSource_InputChanged;
                    _paddleInputSource.PaddleInputReceived -= PaddleInputSource_InputChanged;
                    _paddleInputSource.PaddleInputReceived -= PaddleInputSource_PaddleInputReceived;
                    _telemetryReceiver.PacketReceived -= TelemetryReceiver_PacketReceived;
                    _telemetryStatusTimer.Tick -= TelemetryStatusTimer_Tick;
                    break;

                case ShutdownCleanupStepKind.StopTelemetryStatusTimer:
                    _telemetryStatusTimer.Stop();
                    timersDisposed = true;
                    break;

                case ShutdownCleanupStepKind.StopContinuousPhprRuntime:
                    {
                        var continuousRuntimeStop = await _realPhprContinuousEffectsRuntime
                            .StopAsync(step.Timeout ?? TimeSpan.FromSeconds(2))
                            .ConfigureAwait(false);
                        if (continuousRuntimeStop.SlipLockRuntimeTimedOut)
                        {
                            shutdownExceptions.Add("slipLockRuntime:TimeoutException:slip/lock runtime did not stop within 2 seconds");
                        }

                        if (continuousRuntimeStop.RoadRuntimeTimedOut)
                        {
                            shutdownExceptions.Add("roadRuntime:TimeoutException:road runtime did not stop within 2 seconds");
                        }

                        break;
                    }

                case ShutdownCleanupStepKind.StopStandaloneBst1PulseSession:
                    _hapticPipeline.StopManualAsioHardwareTest("App shutdown stopped standalone BST-1 pulse session.");
                    standalonePulseDisposed = true;
                    break;

                case ShutdownCleanupStepKind.DisposeTestBench:
                    try
                    {
                        await _testBench.DisposeAsync().AsTask().WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        shutdownExceptions.Add($"testBench:{ex.GetType().Name}:{ex.Message}");
                    }

                    break;

                case ShutdownCleanupStepKind.DisposePaddleInputSource:
                    try
                    {
                        await _paddleInputSource.DisposeAsync().AsTask().WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        paddleListenerDisposed = true;
                    }
                    catch (Exception ex)
                    {
                        shutdownExceptions.Add($"paddleListener:{ex.GetType().Name}:{ex.Message}");
                    }

                    break;

                case ShutdownCleanupStepKind.DisposeTelemetryReceiver:
                    try
                    {
                        await _telemetryReceiver.DisposeAsync().AsTask().WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        udpListenerDisposed = true;
                    }
                    catch (Exception ex)
                    {
                        shutdownExceptions.Add($"udpListener:{ex.GetType().Name}:{ex.Message}");
                    }

                    break;

                case ShutdownCleanupStepKind.StopRoadAndDisposeRealPhprOutput:
                    try
                    {
                        await _realRoadVibrationRouter.StopAsync("App shutdown stopped P-HPR road output.")
                            .AsTask()
                            .WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2))
                            .ConfigureAwait(false);
                        await _realPhprOutput.DisposeAsync().AsTask().WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        shutdownExceptions.Add($"phprOutput:{ex.GetType().Name}:{ex.Message}");
                    }

                    break;

                case ShutdownCleanupStepKind.DisposeContinuousPhprRuntime:
                    try
                    {
                        await _realPhprContinuousEffectsRuntime.DisposeAsync().AsTask()
                            .WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2))
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        shutdownExceptions.Add($"phprContinuousRuntime:{ex.GetType().Name}:{ex.Message}");
                    }

                    break;

                case ShutdownCleanupStepKind.DisposeHapticPipeline:
                    try
                    {
                        await _hapticPipeline.DisposeAsync().AsTask().WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                        asioDisposed = true;
                    }
                    catch (Exception ex)
                    {
                        shutdownExceptions.Add($"hapticPipeline:{ex.GetType().Name}:{ex.Message}");
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unknown shutdown cleanup step: {step.Kind}");
            }
        }

        RecordShutdownDiagnostic(
            "shutdown-completed",
            shutdownStartedAtUtc,
            minimizeToTrayEnabled,
            asioDisposed,
            standalonePulseDisposed,
            paddleListenerDisposed,
            udpListenerDisposed,
            timersDisposed,
            pendingTaskCount: _activeReplayTask is { IsCompleted: false } ? 1 : 0,
            shutdownExceptions);
    }

    private void RecordShutdownDiagnostic(
        string eventName,
        DateTimeOffset shutdownStartedAtUtc,
        bool minimizeToTrayEnabled,
        bool asioDisposed,
        bool standalonePulseDisposed,
        bool paddleListenerDisposed,
        bool udpListenerDisposed,
        bool timersDisposed,
        int pendingTaskCount,
        IReadOnlyCollection<string> shutdownExceptions)
    {
        try
        {
            var directory = GetLocalValidationResultsDirectory();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "bst1-asio-pulse-flight-recorder.jsonl");
            var record = new
            {
                sessionId = "app-shutdown",
                pulseId = 0,
                eventName,
                wallClockUtc = DateTimeOffset.UtcNow,
                elapsedMs = (DateTimeOffset.UtcNow - shutdownStartedAtUtc).TotalMilliseconds,
                threadId = Environment.CurrentManagedThreadId,
                source = "shutdown",
                outputMode = _selectedOutputKind.ToString(),
                asioDriver = _selectedAsioDriverName,
                selectedChannel = _selectedAsioOutputChannel,
                asioArmed = _asioArmed,
                shutdownRequested = true,
                minimizeToTrayEnabled,
                asioDisposed,
                standalonePulseSessionDisposed = standalonePulseDisposed,
                paddleListenerDisposed,
                udpListenerDisposed,
                timersDisposed,
                pendingTaskCount,
                shutdownExceptions
            };
            var json = JsonSerializer.Serialize(record);
            using var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(json);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _settingsError = $"Shutdown diagnostic write failed: {ex.Message}";
        }
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

    private sealed record ThemePalette(
        string Background,
        string Chrome,
        string Sidebar,
        string TopBar,
        string Surface,
        string SurfaceAlt,
        string SurfaceRaised,
        string Border,
        string BorderStrong,
        string Text,
        string MutedText,
        string Accent,
        string AccentHover,
        string AccentPressed,
        string AccentSoft,
        string Danger,
        string DangerHover,
        string DangerPressed,
        string Success,
        string Warning,
        string Info,
        string Input,
        string InputFocus,
        string Overlay);
}
