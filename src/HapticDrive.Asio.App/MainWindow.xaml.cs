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
using HapticDrive.Input.Abstractions.Driving;
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
    private readonly IAsioDriverCatalog _asioDriverCatalog = new WindowsRegistryAsioDriverCatalog();
    private readonly AsioDriverVisibilityDiagnostics _asioVisibilityDiagnostics;
    private readonly AsioReadinessDiagnostics _asioReadinessDiagnostics;
    private readonly AppSettingsStore _settingsStore = new();
    private readonly IOutputInterlock _outputInterlock = new OutputInterlock();
    private readonly IPHprWriteAuthorization _phprWriteAuthorization = new PHprSessionWriteAuthorization();
    private readonly AudioTestBench _testBench = new();
    private readonly IInputDeviceDiscovery _inputDeviceDiscovery = new WindowsInputDeviceDiscovery();
    private readonly IWheelInputCandidateProvider _wheelInputCandidateProvider = new WheelInputCandidateProvider();
    private readonly IWheelPaddleInputSource _paddleInputSource = new PollingWheelPaddleInputSource(
        new WindowsGameControllerButtonStateReader());
    private readonly DrivingArmedStateService _drivingArmedStateService = new();
    private readonly ShiftIntentProcessor _shiftIntentProcessor;
    private readonly MockPhprOutputDevice _mockPhprOutput = new();
    private readonly SafetyLimitedPhprOutputDevice _mockPhprSafetyOutput;
    private readonly DeferredWindowsHidReportWriter _realPhprHidWriter;
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
    private readonly SupportBundleExporter _supportBundleExporter = new();
    private readonly SelectedRecordingDetailExporter _selectedRecordingDetailExporter = new();
    private readonly PHprHidOpenCheckRunner _phprHidOpenCheckRunner = new();
    private readonly PHprGearPulseRouter _mockGearPulseRouter;
    private readonly PHprPedalEffectsRouter _mockPedalEffectsRouter;
    private readonly PaddleGearBenchTestController _paddleGearBenchTestController = new(PaddleGearBenchTestOptions.EnabledDirect);
    private readonly IPHprSoftwareCoexistenceDetector _phprSoftwareCoexistenceDetector =
        new PHprSoftwareCoexistenceDetector(new WindowsProcessSnapshotProvider());
    private readonly IPHprDirectOutputCandidateProvider _phprDirectOutputCandidateProvider =
        new WindowsPhprDirectOutputCandidateProvider();
    private readonly HapticProfileStore _profileStore = new();
    private readonly PhprEffectProfileStore _phprProfileStore = new();
    private readonly RuntimeLifecycleCoordinator _runtimeLifecycleCoordinator = new();
    private readonly EffectSettingsListViewModel _effectSettingsViewModel = new();
    private readonly SafetyStateViewModel _safetyStateViewModel = new();
    private readonly TelemetryStatusViewModel _telemetryStatusViewModel = new();
    private readonly OutputDeviceStatusViewModel _outputDeviceStatusViewModel = new();
    private readonly RecordingReplayStatusViewModel _recordingReplayStatusViewModel = new();
    private readonly PhprStatusViewModel _phprStatusViewModel = new();
    private readonly DiagnosticsSummaryViewModel _diagnosticsSummaryViewModel = new();
    private readonly ApplicationSafetyController _applicationSafetyController;
    private readonly TelemetrySessionController _telemetrySessionController;
    private readonly AudioOutputController _audioOutputController;
    private readonly RecordingReplayController _recordingReplayController;
    private readonly PhprOutputController _phprOutputController;
    private readonly DiagnosticsPresentationController _diagnosticsPresentationController;
    private readonly ProfileTuningController _profileTuningController;
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
            "Operational overview for haptics, telemetry, and hardware readiness.",
            "Ready overview",
            [
                "Null output stays the safe default until you choose hardware.",
                "Recording and forwarding preserve the incoming F1 25 UDP packets.",
                "P-HPR direct mode never starts output on launch."
            ]),
        new(
            "Devices",
            "Devices",
            "Bass shaker output, Simagic P-HPR pedals, and wheel paddle setup.",
            "Device setup and readiness",
            [
                "ASIO absence does not block startup, builds, or tests.",
                "P-HPR mock mode never writes hardware reports.",
                "Wheel paddles can drive local gear pulses after mapping and safety checks."
            ]),
        new(
            "Effects",
            "Effects",
            "Normal tuning for the seat shaker and pedal effects.",
            "Effect tuning ready",
            [
                "Engine, gear shift, kerb, impact, road texture, slip, and brake-lock effects stay grouped by hardware.",
                "P-HPR pedal effects remain separate from the BST-1 audio path.",
                "Physical tuning waits for local hardware validation."
            ]),
        new(
            "Routing / Mixer",
            "Routing / Mixer",
            "Output routing, gain, mute, active effects, and protection status.",
            "Mixer summary ready",
            [
                "ASIO/BST-1 routing remains audio-only.",
                "P-HPR modules use a separate actuator path.",
                "Emergency mute and limiter protection stay visible."
            ]),
        new(
            "Telemetry / UDP",
            "Telemetry / UDP",
            "F1 25 UDP input, forwarding, recording, and replay.",
            "Telemetry tools ready",
            [
                "Default listen port is 20778.",
                "Forwarding sends exact packet bytes to enabled destinations.",
                "Recording captures the incoming UDP packets before parser validation.",
                "Replay uses recorded packets for software-only testing."
            ]),
        new(
            "Profiles",
            "Profiles",
            "Saved tuning profiles and app preferences.",
            "Profiles ready",
            [
                "The default profile uses the current software defaults for this rig.",
                "Audio tuning auto-saves to the default profile under local app data.",
                "Profile values are validated and repaired to the current software ranges on load.",
                "Emergency state and live hardware state stay runtime-only.",
                "Device settings remain separate from effect profiles."
            ]),
        new(
            "Testing / Validation",
            "Testing / Validation",
            "Manual tests, synthetic checks, and local validation exports.",
            "Testing tools ready",
            [
                "Synthetic validation stays available without physical hardware.",
                "Manual BST-1 and P-HPR pulse checks live here.",
                "Bench and export tools remain local-only and do not claim physical validation."
            ]),
        new(
            "Advanced / Diagnostics",
            "Advanced / Diagnostics",
            "Developer diagnostics, raw direct-control details, and guarded research controls.",
            "Advanced diagnostics hidden until enabled",
            [
                "Raw P-HPR direct-control and mock-routing internals stay collapsed by default.",
                "Testing tools now live on Testing / Validation.",
                "Copyable diagnostics keep parser, mixer, output, input, and P-HPR safety state visible."
            ])
    ];

    private bool _hapticsStarted;
    private bool _emergencyMuted;
    private bool _lightTheme;
    private bool _allowLanTelemetry;
    private bool _updatingOutputUi;
    private bool _startupAsioDefaultsApplied;
    private bool _shutdownCleanupStarted;
    private bool _shutdownCleanupCompleted;
    private string _selectedGameId = GameTelemetryCatalog.DefaultGameId;
    private AudioOutputDeviceKind _selectedOutputKind = AudioOutputDeviceKind.Null;
    private string? _selectedAsioDriverName;
    private int? _selectedAsioOutputChannel;
    private bool _asioArmed;
    private string? _telemetryStartError;
    private string? _telemetryListenerWarning;
    private string? _recordingError;
    private string? _replayError;
    private string? _settingsError;
    private bool _hasPersistedOutputModePreference;
    private bool _phprPedalsEnabledPreference;
    private PhprPedalsModePreference _phprPedalsModePreference = PhprPedalsModePreference.Mock;
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
    private DrivingArmedState _lastDrivingArmedState = DrivingArmedState.Default;
    private ActuationDrivingContext _lastActuationDrivingContext = ActuationDrivingContextFactory.SafeDefault("startup");
    private string _recordingLibraryFilterText = string.Empty;
    private CancellationTokenSource? _recordingLibraryAnalysisCts;
    private int _telemetryStatusTickInFlight;
    private long _telemetryStatusTickSkippedCount;
    private readonly string _roadTextureFlightRecorderSessionId = Guid.NewGuid().ToString("N");
    private readonly string _appSessionCorrelationId = Guid.NewGuid().ToString("N");
    private IRoadTextureFlightRecorder _roadTextureFlightRecorder = DisabledRoadTextureFlightRecorder.Instance;
    private DateTimeOffset? _lastPhprCoexistenceScanUtc;
    private string? _lastPhprValidationExportPath;
    private string? _lastSupportBundleExportPath;
    private string? _telemetrySessionCorrelationId;
    private string? _telemetrySessionFingerprint;
    private string? _recordingSessionCorrelationId;
    private string? _recordingSessionFingerprint;
    private string _outputDeviceSessionCorrelationId = Guid.NewGuid().ToString("N");
    private string? _outputDeviceSessionFingerprint;
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
    private List<string> _allowedTelemetryRemoteAddresses = [];
    private List<ForwardingDestinationListItem> _forwardingDestinationItems = [];
    private List<PaddleDeviceListItem> _paddleDeviceItems = [];
    private List<PhprDirectOutputCandidateListItem> _realPhprCandidateItems = [];
    private List<RecordingLibraryItem> _recordingLibraryItems = [];
    private List<RecordingLibraryItem> _filteredRecordingLibraryItems = [];
    private readonly Dictionary<string, string> _recordingLibraryHistogramTextByPath = new(StringComparer.OrdinalIgnoreCase);
    private IUdpTelemetryReceiver _telemetryReceiver;
    private TelemetryIngressWorker _telemetryIngressWorker;

    public MainWindow()
    {
        _asioVisibilityDiagnostics = new AsioDriverVisibilityDiagnostics(_asioDriverCatalog);
        _asioReadinessDiagnostics = new AsioReadinessDiagnostics(_asioDriverCatalog);
        _applicationSafetyController = new ApplicationSafetyController(_safetyStateViewModel);
        _telemetrySessionController = new TelemetrySessionController(_telemetryStatusViewModel);
        _audioOutputController = new AudioOutputController(_outputDeviceStatusViewModel);
        _recordingReplayController = new RecordingReplayController(_recordingReplayStatusViewModel);
        _phprOutputController = new PhprOutputController(_phprStatusViewModel);
        _diagnosticsPresentationController = new DiagnosticsPresentationController(_diagnosticsSummaryViewModel);

        var appSettings = AppSettingsSnapshotBuilder.BuildHydrationSnapshot(_settingsStore.Load());
        _settingsError = appSettings.SettingsError;
        _lightTheme = appSettings.UseLightTheme;
        _advancedDiagnosticsEnabled = appSettings.AdvancedDiagnosticsEnabled;
        _selectedGameId = appSettings.SelectedGameId;
        _allowLanTelemetry = appSettings.AllowLanTelemetry;
        _allowedTelemetryRemoteAddresses = appSettings.AllowedTelemetryRemoteAddresses.ToList();
        _hasPersistedOutputModePreference = appSettings.HasPersistedOutputModePreference;
        _phprPedalsEnabledPreference = appSettings.PhprPedalsEnabledPreference;
        _phprPedalsModePreference = appSettings.PhprPedalsModePreference;
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
        _realPhprHidWriter = new DeferredWindowsHidReportWriter(
            selector => new WindowsHidReportWriter(selector),
            _outputInterlock,
            _phprWriteAuthorization);
        _realPhprOutput = new SimagicPhprOutputDevice(
            _realPhprHidWriter,
            _realPhprOptions,
            _outputInterlock,
            _phprWriteAuthorization);
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
        ApplyPersistedPhprPedalsPreferenceToRuntime(saveSafeSettings: false, updateUi: false);
        LoadPersistedAudioProfile();
        _hapticPipeline = CreatePipelineForSelectedOutput();
        _telemetryReceiver = CreateTelemetryReceiver();
        _telemetryIngressWorker = CreateTelemetryIngressWorker(_hapticPipeline);
        SyncOutputInterlockState(_outputInterlock.Current);
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
        EffectsViewControl.DataContext = _effectSettingsViewModel;

        EffectsViewControl.TuningControlChanged += TuningControl_Changed;
        EffectsViewControl.PhprPedalsControlChanged += PhprPedalsControl_Changed;
        EffectsViewControl.PhprPedalsControlLostFocus += PhprPedalsControl_LostFocus;
        EffectsViewControl.RealPhprDirectControlChanged += RealPhprDirectControlCheckBox_Changed;
        EffectsViewControl.RealPhprDirectControlLostFocus += RealPhprDirectControl_LostFocus;
        EffectsViewControl.Bst1PaddleGearPulseControlChanged += Bst1PaddleGearPulseControl_Changed;
        EffectsViewControl.Bst1PaddleGearPulseControlLostFocus += Bst1PaddleGearPulseControl_LostFocus;
        RoutingMixerViewControl.TuningControlChanged += TuningControl_Changed;
        TelemetryUdpViewControl.ReplayTimingModeSelectionChanged += ReplayTimingModeComboBox_SelectionChanged;
        TelemetryUdpViewControl.StartRecordingClicked += StartRecordingButton_Click;
        TelemetryUdpViewControl.StartReplayClicked += StartReplayButton_Click;
        TelemetryUdpViewControl.ReplaySelectedRecordingClicked += ReplaySelectedRecordingButton_Click;
        TelemetryUdpViewControl.RefreshRecordingsClicked += RefreshRecordingsButton_Click;
        TelemetryUdpViewControl.DeleteSelectedRecordingClicked += DeleteSelectedRecordingButton_Click;
        TelemetryUdpViewControl.RenameSelectedRecordingClicked += RenameSelectedRecordingButton_Click;
        TelemetryUdpViewControl.CopySelectedRecordingDetailClicked += CopySelectedRecordingDetailButton_Click;
        TelemetryUdpViewControl.ExportSelectedRecordingDetailClicked += ExportSelectedRecordingDetailButton_Click;
        TelemetryUdpViewControl.RecordingLibraryFilterTextChanged += RecordingLibraryFilterTextBox_TextChanged;
        TelemetryUdpViewControl.ClearRecordingLibraryFilterClicked += ClearRecordingLibraryFilterButton_Click;
        TelemetryUdpViewControl.RecordingLibrarySelectionChanged += RecordingLibraryListBox_SelectionChanged;
        TelemetryUdpViewControl.AllowLanTelemetryChanged += AllowLanTelemetryCheckBox_Changed;
        TelemetryUdpViewControl.AllowedRemoteAddressesLostFocus += AllowedRemoteAddressesTextBox_LostFocus;
        TelemetryUdpViewControl.SaveForwardingDestinationClicked += SaveForwardingDestinationButton_Click;
        TelemetryUdpViewControl.RemoveForwardingDestinationClicked += RemoveForwardingDestinationButton_Click;
        TelemetryUdpViewControl.ClearForwardingDestinationClicked += ClearForwardingDestinationButton_Click;
        TelemetryUdpViewControl.ForwardingDestinationsSelectionChanged += ForwardingDestinationsListBox_SelectionChanged;
        ProfilesViewControl.ProfileNameLostFocus += ProfileNameTextBox_LostFocus;
        ProfilesViewControl.SaveProfileClicked += SaveProfileButton_Click;
        ProfilesViewControl.LoadProfileClicked += LoadProfileButton_Click;
        ProfilesViewControl.ResetProfileClicked += ResetProfileButton_Click;
        TestingValidationViewControl.TestBenchSignalSelectionChanged += TestBenchSignalComboBox_SelectionChanged;
        TestingValidationViewControl.TestBenchStartStopClicked += TestBenchStartStopButton_Click;
        TestingValidationViewControl.ManualBst1ControlLostFocus += ManualBst1Control_LostFocus;
        TestingValidationViewControl.ManualBst1PulseClicked += ManualBst1PulseButton_Click;
        TestingValidationViewControl.ManualAsioHardwareTestChannel1Clicked += ManualAsioHardwareTestChannel1Button_Click;
        TestingValidationViewControl.ManualAsioHardwareTestChannel0Clicked += ManualAsioHardwareTestChannel0Button_Click;
        TestingValidationViewControl.TestPhprBrakePulseClicked += TestPhprBrakePulseButton_Click;
        TestingValidationViewControl.TestPhprThrottlePulseClicked += TestPhprThrottlePulseButton_Click;
        TestingValidationViewControl.LocalGearTestModeChanged += LocalGearTestModeCheckBox_Changed;
        TestingValidationViewControl.StartGearTestListenerClicked += StartGearTestListenerButton_Click;
        TestingValidationViewControl.PaddleGearBenchControlChanged += PaddleGearBenchControl_Changed;
        TestingValidationViewControl.PaddleGearBenchSelectionChanged += PaddleGearBenchControl_Changed;
        TestingValidationViewControl.PaddleGearBenchControlLostFocus += PaddleGearBenchControl_LostFocus;
        TestingValidationViewControl.ClearPaddleGearBenchCountersClicked += ClearPaddleGearBenchCountersButton_Click;
        TestingValidationViewControl.PhprValidationControlChanged += PhprValidationControl_Changed;
        TestingValidationViewControl.PhprValidationControlLostFocus += PhprValidationControl_LostFocus;
        TestingValidationViewControl.RefreshPhprValidationChecklistClicked += RefreshPhprValidationChecklistButton_Click;
        TestingValidationViewControl.ExportPhprValidationResultClicked += ExportPhprValidationResultButton_Click;
        AdvancedDiagnosticsViewControl.AdvancedDiagnosticsEnabledChanged += AdvancedDiagnosticsEnabledCheckBox_Changed;
        AdvancedDiagnosticsViewControl.RealPhprDirectControlChanged += RealPhprDirectControlCheckBox_Changed;
        AdvancedDiagnosticsViewControl.RealPhprDirectControlSelectionChanged += RealPhprDirectControlCheckBox_Changed;
        AdvancedDiagnosticsViewControl.RefreshRealPhprCandidatesClicked += RefreshRealPhprCandidatesButton_Click;
        AdvancedDiagnosticsViewControl.DryRunRealPhprSelectionClicked += DryRunRealPhprSelectionButton_Click;
        AdvancedDiagnosticsViewControl.OpenCheckRealPhprSelectionClicked += OpenCheckRealPhprSelectionButton_Click;
        AdvancedDiagnosticsViewControl.RealPhprCandidateSelectionChanged += RealPhprCandidateComboBox_SelectionChanged;
        AdvancedDiagnosticsViewControl.ApplyRealPhprSelectionClicked += ApplyRealPhprSelectionButton_Click;
        AdvancedDiagnosticsViewControl.AuthorizeRealPhprWritesClicked += AuthorizeRealPhprWritesButton_Click;
        AdvancedDiagnosticsViewControl.RealPhprDirectControlLostFocus += RealPhprDirectControl_LostFocus;
        AdvancedDiagnosticsViewControl.TestRealPhprBrakePulseClicked += TestRealPhprBrakePulseButton_Click;
        AdvancedDiagnosticsViewControl.TestRealPhprThrottlePulseClicked += TestRealPhprThrottlePulseButton_Click;
        AdvancedDiagnosticsViewControl.RealPhprEmergencyStopClicked += RealPhprEmergencyStopButton_Click;
        AdvancedDiagnosticsViewControl.ClearRealPhprEmergencyStopClicked += ClearRealPhprEmergencyStopButton_Click;
        AdvancedDiagnosticsViewControl.MockGearPulseControlChanged += MockGearPulseControl_Changed;
        AdvancedDiagnosticsViewControl.MockGearPulseControlSelectionChanged += MockGearPulseControl_Changed;
        AdvancedDiagnosticsViewControl.MockGearPulseControlLostFocus += MockGearPulseControl_LostFocus;
        AdvancedDiagnosticsViewControl.ClearMockGearPulseDiagnosticsClicked += ClearMockGearPulseDiagnosticsButton_Click;
        AdvancedDiagnosticsViewControl.MockGearPulseEmergencyStopClicked += MockGearPulseEmergencyStopButton_Click;
        AdvancedDiagnosticsViewControl.ClearMockGearPulseEmergencyStopClicked += ClearMockGearPulseEmergencyStopButton_Click;
        AdvancedDiagnosticsViewControl.MockPedalEffectsControlChanged += MockPedalEffectsControl_Changed;
        AdvancedDiagnosticsViewControl.MockPedalEffectsControlSelectionChanged += MockPedalEffectsControl_Changed;
        AdvancedDiagnosticsViewControl.MockPedalEffectsControlLostFocus += MockPedalEffectsControl_LostFocus;
        AdvancedDiagnosticsViewControl.ClearMockPedalEffectsDiagnosticsClicked += ClearMockPedalEffectsDiagnosticsButton_Click;
        AdvancedDiagnosticsViewControl.MockPedalEffectsEmergencyStopClicked += MockPedalEffectsEmergencyStopButton_Click;
        AdvancedDiagnosticsViewControl.ClearMockPedalEffectsEmergencyStopClicked += ClearMockPedalEffectsEmergencyStopButton_Click;
        AdvancedDiagnosticsViewControl.ThemeSettingChanged += ThemeSettingCheckBox_Changed;
        AdvancedDiagnosticsViewControl.ResetProfileClicked += ResetProfileButton_Click;
        AdvancedDiagnosticsViewControl.RefreshDiagnosticsClicked += RefreshDiagnosticsButton_Click;
        AdvancedDiagnosticsViewControl.ExportSupportBundleClicked += ExportSupportBundleButton_Click;
        AdvancedDiagnosticsViewControl.CopyDiagnosticsClicked += CopyDiagnosticsButton_Click;
        AdvancedDiagnosticsViewControl.RoadTextureFlightRecorderChanged += RoadTextureFlightRecorderCheckBox_Changed;
        DevicesViewControl.OutputModeSelectionChanged += OutputModeComboBox_SelectionChanged;
        DevicesViewControl.RefreshAsioClicked += RefreshAsioButton_Click;
        DevicesViewControl.AsioDriverSelectionChanged += AsioDriverComboBox_SelectionChanged;
        DevicesViewControl.AsioOutputChannelSelectionChanged += AsioOutputChannelComboBox_SelectionChanged;
        DevicesViewControl.AsioArmChanged += AsioArmCheckBox_Changed;
        DevicesViewControl.PhprPedalsControlChanged += PhprPedalsControl_Changed;
        DevicesViewControl.PhprPedalsModeSelectionChanged += PhprPedalsModeComboBox_SelectionChanged;
        DevicesViewControl.PhprPedalsEmergencyStopClicked += PhprPedalsEmergencyStopButton_Click;
        DevicesViewControl.ClearPhprPedalsEmergencyStopClicked += ClearPhprPedalsEmergencyStopButton_Click;
        DevicesViewControl.PhprPedalsStopAllClearDeviceStateClicked += PhprPedalsStopAllClearDeviceStateButton_Click;
        DevicesViewControl.RefreshInputDevicesClicked += RefreshInputDevicesButton_Click;
        DevicesViewControl.PaddleInputDeviceSelectionChanged += PaddleInputDeviceComboBox_SelectionChanged;
        DevicesViewControl.StartPaddleInputListenerClicked += StartPaddleInputListenerButton_Click;
        DevicesViewControl.StopPaddleInputListenerClicked += StopPaddleInputListenerButton_Click;
        DevicesViewControl.PaddleMappingLostFocus += PaddleMappingControl_LostFocus;
        DevicesViewControl.SetLeftPaddleFromLastChangedClicked += SetLeftPaddleFromLastChangedButton_Click;
        DevicesViewControl.SetRightPaddleFromLastChangedClicked += SetRightPaddleFromLastChangedButton_Click;
        DevicesViewControl.ShiftIntentEnabledChanged += ShiftIntentEnabledCheckBox_Changed;
        DevicesViewControl.ShiftIntentModeSelectionChanged += ShiftIntentModeComboBox_SelectionChanged;
        DevicesViewControl.ClearShiftIntentDiagnosticsClicked += ClearShiftIntentDiagnosticsButton_Click;

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
        ApplyTheme(_lightTheme);
        ApplyTelemetryListenerSettingsToControls();
        RefreshForwardingDestinationItems();
        ApplyProfileToControls(_currentProfile);
        ApplyProfileToRuntime(_currentProfile);
        _profileTuningController = new ProfileTuningController(
            _effectSettingsViewModel,
            BuiltInHapticEffectRegistry.Instance,
            () => _currentProfile,
            () => _hapticsStarted,
            profile => _currentProfile = profile,
            ApplyProfileToRuntime,
            UpdateProfileControlText,
            (profile, cancellationToken) => _profileStore.SaveAsync(profile, HapticProfileStore.GetDefaultProfilePath(), cancellationToken),
            PublishProfileTuningFeedback,
            message => FooterStatusText.Text = message);
        _profileTuningController.RefreshEffectSettings(_currentProfile);
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
            await _telemetryIngressWorker.StartAsync();
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
        PublishRuntimeControlSnapshot();
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

    private void ApplyTelemetryListenerSettingsToControls()
    {
        AllowLanTelemetryCheckBox.IsChecked = _allowLanTelemetry;
        AllowedRemoteAddressesTextBox.Text = string.Join(", ", _allowedTelemetryRemoteAddresses);
        UpdateTelemetryListenerWarning();
    }

    private void UpdateTelemetryListenerWarning()
    {
        _telemetryListenerWarning = _allowLanTelemetry && _allowedTelemetryRemoteAddresses.Count == 0
            ? "LAN telemetry is enabled without an IP allowlist. Any sender on the LAN can reach the listener until you add allowed remote IPs."
            : null;
    }

    private IUdpTelemetryReceiver CreateTelemetryReceiver()
    {
        return new UdpTelemetryReceiver(BuildTelemetryReceiverOptions());
    }

    private TelemetryIngressWorker CreateTelemetryIngressWorker(HapticPipelineCoordinator pipeline)
    {
        return new TelemetryIngressWorker(
            pipeline.ProcessLiveTelemetryPacket,
            () => pipeline.RecordingService.GetSnapshot().IsRecording,
            pipeline.RecordingService.RecordPacket,
            pipeline.RecordingService.MarkIncomplete,
            () => pipeline.TelemetryForwarder.GetSnapshot().IsEnabled,
            pipeline.TelemetryForwarder.ForwardAsync);
    }

    private UdpTelemetryReceiverOptions BuildTelemetryReceiverOptions()
    {
        var allowlist = _allowedTelemetryRemoteAddresses
            .Select(IPAddress.Parse)
            .ToHashSet();

        return new UdpTelemetryReceiverOptions(
            Port: UdpTelemetryReceiverOptions.DefaultPort,
            AllowLanTelemetry: _allowLanTelemetry,
            AllowedRemoteAddresses: allowlist.Count == 0 ? null : allowlist);
    }

    private static bool TryParseTelemetryRemoteAddresses(
        string? rawValue,
        out List<string> normalizedAddresses,
        out string message)
    {
        normalizedAddresses = [];
        var tokens = (rawValue ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (!IPAddress.TryParse(token, out _))
            {
                message = $"Allowed remote IP '{token}' is not a valid IP address.";
                return false;
            }

            if (!normalizedAddresses.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                normalizedAddresses.Add(token);
            }
        }

        message = normalizedAddresses.Count == 0
            ? "LAN telemetry allowlist cleared. Loopback-only remains the safest default."
            : $"LAN telemetry allowlist saved with {normalizedAddresses.Count:N0} remote IP(s).";
        return true;
    }

    private void PublishRuntimeControlSnapshot()
    {
        var pipelineSnapshot = _hapticPipeline.GetSnapshot();
        var manualSnapshot = pipelineSnapshot.ManualAsioHardwareTest;
        _runtimeLifecycleCoordinator.PublishSnapshot(new HapticRuntimeControlSnapshot(
            IsMuted: pipelineSnapshot.EmergencyMute,
            OutputInterlockGeneration: _outputInterlock.Current.Generation,
            SelectedOutputId: BuildSelectedOutputId(),
            ActiveProfileId: _currentProfile.Name,
            ActiveProfileHash: ComputeProfileHash(_currentProfile),
            SampleRate: _hapticPipeline.Format.SampleRate,
            BufferSize: _hapticPipeline.Format.FrameCount,
            ManualTestActive: manualSnapshot.IsActive,
            TelemetryFreshnessPolicy.Default,
            DateTimeOffset.UtcNow));
    }

    private async Task RunSerializedLifecycleOperationAsync(
        Func<long, CancellationToken, Task> operation,
        string failureContext)
    {
        try
        {
            await _runtimeLifecycleCoordinator.RunSerializedAsync(operation).ConfigureAwait(true);
            PublishRuntimeControlSnapshot();
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = $"{failureContext}: {ex.Message}";
        }
    }

    private string BuildSelectedOutputId()
    {
        return _selectedOutputKind switch
        {
            AudioOutputDeviceKind.Asio => $"asio:{_selectedAsioDriverName ?? "unselected"}:{_selectedAsioOutputChannel?.ToString(CultureInfo.InvariantCulture) ?? "none"}",
            AudioOutputDeviceKind.WasapiDebug => "wasapi-debug",
            _ => "null"
        };
    }

    private SupportBundleCorrelationIds CaptureSupportBundleCorrelationIds(HapticPipelineSnapshot pipelineSnapshot)
    {
        ArgumentNullException.ThrowIfNull(pipelineSnapshot);

        UpdateTelemetrySessionCorrelation(pipelineSnapshot);
        UpdateRecordingSessionCorrelation(pipelineSnapshot.Recording);
        UpdateOutputSessionCorrelation(BuildSelectedOutputId());

        return new SupportBundleCorrelationIds(
            _appSessionCorrelationId,
            _telemetrySessionCorrelationId,
            _recordingSessionCorrelationId,
            _outputDeviceSessionCorrelationId);
    }

    private void UpdateTelemetrySessionCorrelation(HapticPipelineSnapshot pipelineSnapshot)
    {
        var identity = pipelineSnapshot.HapticFrame?.Identity;
        var source = identity?.Source ?? pipelineSnapshot.VehicleState.Frame.Source;
        var sessionUid = identity?.SessionUid ?? pipelineSnapshot.VehicleState.Frame.SessionUid;
        var playerCarIndex = identity?.PlayerCarIndex ?? pipelineSnapshot.VehicleState.Frame.PlayerCarIndex;
        if (string.IsNullOrWhiteSpace(source) && sessionUid is null && playerCarIndex is null)
        {
            return;
        }

        var fingerprint = $"{source ?? "unknown"}|{sessionUid?.ToString(CultureInfo.InvariantCulture) ?? "none"}|{playerCarIndex?.ToString(CultureInfo.InvariantCulture) ?? "none"}";
        if (!string.Equals(_telemetrySessionFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _telemetrySessionFingerprint = fingerprint;
            _telemetrySessionCorrelationId = Guid.NewGuid().ToString("N");
        }
    }

    private void UpdateRecordingSessionCorrelation(TelemetryRecordingSnapshot recordingSnapshot)
    {
        ArgumentNullException.ThrowIfNull(recordingSnapshot);

        if (!recordingSnapshot.IsRecording || string.IsNullOrWhiteSpace(recordingSnapshot.FilePath))
        {
            _recordingSessionFingerprint = null;
            _recordingSessionCorrelationId = null;
            return;
        }

        var fingerprint = recordingSnapshot.FilePath.Trim();
        if (!string.Equals(_recordingSessionFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            _recordingSessionFingerprint = fingerprint;
            _recordingSessionCorrelationId = Guid.NewGuid().ToString("N");
        }
    }

    private void UpdateOutputSessionCorrelation(string outputId)
    {
        if (!string.Equals(_outputDeviceSessionFingerprint, outputId, StringComparison.Ordinal))
        {
            _outputDeviceSessionFingerprint = outputId;
            _outputDeviceSessionCorrelationId = Guid.NewGuid().ToString("N");
        }
    }

    private static string ComputeProfileHash(HapticDriveProfile profile)
    {
        var json = JsonSerializer.Serialize(profile);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
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
            EffectsViewControl.Visibility = page.NavigationLabel == "Effects"
                ? Visibility.Visible
                : Visibility.Collapsed;
            RoutingMixerViewControl.Visibility = page.NavigationLabel == "Routing / Mixer"
                ? Visibility.Visible
                : Visibility.Collapsed;
            DevicesViewControl.Visibility = page.NavigationLabel == "Devices"
                ? Visibility.Visible
                : Visibility.Collapsed;
            var isTelemetryPage = page.NavigationLabel == "Telemetry / UDP";
            var isTestingPage = page.NavigationLabel == "Testing / Validation";
            var isProfilesPage = page.NavigationLabel == "Profiles";
            TelemetryUdpViewControl.Visibility = isTelemetryPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            ProfilesViewControl.Visibility = isProfilesPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            TestingValidationViewControl.Visibility = isTestingPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            var isAdvancedPage = page.NavigationLabel == "Advanced / Diagnostics";
            AdvancedDiagnosticsViewControl.Visibility = isAdvancedPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            AdvancedPhprDiagnosticsPanel.Visibility = isAdvancedPage
                ? Visibility.Visible
                : Visibility.Collapsed;
            SettingsPanel.Visibility = isAdvancedPage && _advancedDiagnosticsEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            DiagnosticsPanel.Visibility = isAdvancedPage && _advancedDiagnosticsEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            FooterStatusText.Text = $"Viewing {page.NavigationLabel}";
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
            UpdateDashboardStatus();
            if (isTelemetryPage)
            {
                _ = RefreshRecordingLibraryAsync();
            }
        }
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

        if (_shutdownCleanupCompleted)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_shutdownCleanupStarted)
        {
            return;
        }

        _shutdownCleanupStarted = true;
        _outputInterlock.Trip(OutputInterlockReason.Shutdown, "Application shutdown requested.");
        RevokePhprWriteAuthorization("Application shutdown requested.");
        SyncOutputInterlockState(_outputInterlock.Current);
        IsEnabled = false;
        FooterStatusText.Text = "Shutting down ASIO and listener resources...";
        _ = ShutdownThenCloseAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private async Task ShutdownThenCloseAsync()
    {
        var shutdownTimeout = TimeSpan.FromSeconds(5);
        try
        {
            var cleanupTask = _runtimeLifecycleCoordinator.RunShutdownAsync(
                _outputInterlock,
                (_, _) => RunShutdownCleanupAsync()).AsTask();
            if (await Task.WhenAny(cleanupTask, Task.Delay(shutdownTimeout)).ConfigureAwait(true) != cleanupTask)
            {
                FooterStatusText.Text = "Shutdown timed out after 5 seconds; closing with best-effort cleanup.";
                RecordShutdownDiagnostic(
                    "shutdown-cleanup-timeout",
                    DateTimeOffset.UtcNow,
                    minimizeToTrayEnabled: false,
                    asioDisposed: false,
                    standalonePulseDisposed: false,
                    paddleListenerDisposed: false,
                    udpListenerDisposed: false,
                    timersDisposed: false,
                    pendingTaskCount: _activeReplayTask is { IsCompleted: false } ? 1 : 0,
                    ["warning: shutdown exceeded 5 seconds; using best-effort cleanup"]);
                await PerformBestEffortShutdownAsync().ConfigureAwait(true);
            }
            else
            {
                await cleanupTask.ConfigureAwait(true);
            }
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

    private async Task PerformBestEffortShutdownAsync()
    {
        _telemetryStatusTimer.Stop();

        try
        {
            await _telemetryIngressWorker.DisposeAsync().ConfigureAwait(true);
        }
        catch
        {
        }

        try
        {
            await _telemetryReceiver.DisposeAsync().ConfigureAwait(true);
        }
        catch
        {
        }

        try
        {
            await _hapticPipeline.DisposeAsync().ConfigureAwait(true);
        }
        catch
        {
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

        _outputInterlock.Trip(OutputInterlockReason.Shutdown, "Application shutdown requested.");
        RevokePhprWriteAuthorization("Application shutdown requested.");
        SyncOutputInterlockState(_outputInterlock.Current);
        await SyncGlobalPhprOutputInterlockAsync(_outputInterlock.Current);

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
                        await _telemetryIngressWorker.DisposeAsync().AsTask().WaitAsync(step.Timeout ?? TimeSpan.FromSeconds(2)).ConfigureAwait(false);
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

