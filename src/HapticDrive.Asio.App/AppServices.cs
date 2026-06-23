using HapticDrive.Actuation.Driving;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Readiness;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Validation;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed class AppServices
{
    public AppServices(
        EffectSettingsListViewModel effectSettingsViewModel,
        SafetyStateViewModel safetyStateViewModel,
        TelemetryStatusViewModel telemetryStatusViewModel,
        OutputDeviceStatusViewModel outputDeviceStatusViewModel,
        RecordingReplayStatusViewModel recordingReplayStatusViewModel,
        PhprStatusViewModel phprStatusViewModel,
        DiagnosticsSummaryViewModel diagnosticsSummaryViewModel,
        ApplicationSafetyController applicationSafetyController,
        TelemetrySessionController telemetrySessionController,
        AudioOutputController audioOutputController,
        RecordingReplayController recordingReplayController,
        PhprOutputController phprOutputController,
        DiagnosticsPresentationController diagnosticsPresentationController,
        AppSettingsStore settingsStore,
        AppSettingsHydrationSnapshot settingsHydrationSnapshot,
        IAsioDriverCatalog asioDriverCatalog,
        AsioDriverVisibilityDiagnostics asioVisibilityDiagnostics,
        AsioReadinessDiagnostics asioReadinessDiagnostics,
        IOutputInterlock outputInterlock,
        IPHprWriteAuthorization phprWriteAuthorization,
        AudioTestBench testBench,
        IInputDeviceDiscovery inputDeviceDiscovery,
        IWheelInputCandidateProvider wheelInputCandidateProvider,
        IWheelPaddleInputSource paddleInputSource,
        DrivingArmedStateService drivingArmedStateService,
        MockPhprOutputDevice mockPhprOutput,
        PHprManualValidationResultExporter phprValidationExporter,
        SupportBundleExporter supportBundleExporter,
        SelectedRecordingDetailExporter selectedRecordingDetailExporter,
        PHprHidOpenCheckRunner phprHidOpenCheckRunner,
        PaddleGearBenchTestController paddleGearBenchTestController,
        IPHprSoftwareCoexistenceDetector phprSoftwareCoexistenceDetector,
        IPHprDirectOutputCandidateProvider phprDirectOutputCandidateProvider,
        HapticProfileStore profileStore,
        PhprEffectProfileStore phprProfileStore,
        RuntimeLifecycleCoordinator runtimeLifecycleCoordinator)
    {
        EffectSettingsViewModel = effectSettingsViewModel ?? throw new ArgumentNullException(nameof(effectSettingsViewModel));
        SafetyStateViewModel = safetyStateViewModel ?? throw new ArgumentNullException(nameof(safetyStateViewModel));
        TelemetryStatusViewModel = telemetryStatusViewModel ?? throw new ArgumentNullException(nameof(telemetryStatusViewModel));
        OutputDeviceStatusViewModel = outputDeviceStatusViewModel ?? throw new ArgumentNullException(nameof(outputDeviceStatusViewModel));
        RecordingReplayStatusViewModel = recordingReplayStatusViewModel ?? throw new ArgumentNullException(nameof(recordingReplayStatusViewModel));
        PhprStatusViewModel = phprStatusViewModel ?? throw new ArgumentNullException(nameof(phprStatusViewModel));
        DiagnosticsSummaryViewModel = diagnosticsSummaryViewModel ?? throw new ArgumentNullException(nameof(diagnosticsSummaryViewModel));
        ApplicationSafetyController = applicationSafetyController ?? throw new ArgumentNullException(nameof(applicationSafetyController));
        TelemetrySessionController = telemetrySessionController ?? throw new ArgumentNullException(nameof(telemetrySessionController));
        AudioOutputController = audioOutputController ?? throw new ArgumentNullException(nameof(audioOutputController));
        RecordingReplayController = recordingReplayController ?? throw new ArgumentNullException(nameof(recordingReplayController));
        PhprOutputController = phprOutputController ?? throw new ArgumentNullException(nameof(phprOutputController));
        DiagnosticsPresentationController = diagnosticsPresentationController ?? throw new ArgumentNullException(nameof(diagnosticsPresentationController));
        SettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        SettingsHydrationSnapshot = settingsHydrationSnapshot ?? throw new ArgumentNullException(nameof(settingsHydrationSnapshot));
        AsioDriverCatalog = asioDriverCatalog ?? throw new ArgumentNullException(nameof(asioDriverCatalog));
        AsioVisibilityDiagnostics = asioVisibilityDiagnostics ?? throw new ArgumentNullException(nameof(asioVisibilityDiagnostics));
        AsioReadinessDiagnostics = asioReadinessDiagnostics ?? throw new ArgumentNullException(nameof(asioReadinessDiagnostics));
        OutputInterlock = outputInterlock ?? throw new ArgumentNullException(nameof(outputInterlock));
        PhprWriteAuthorization = phprWriteAuthorization ?? throw new ArgumentNullException(nameof(phprWriteAuthorization));
        TestBench = testBench ?? throw new ArgumentNullException(nameof(testBench));
        InputDeviceDiscovery = inputDeviceDiscovery ?? throw new ArgumentNullException(nameof(inputDeviceDiscovery));
        WheelInputCandidateProvider = wheelInputCandidateProvider ?? throw new ArgumentNullException(nameof(wheelInputCandidateProvider));
        PaddleInputSource = paddleInputSource ?? throw new ArgumentNullException(nameof(paddleInputSource));
        DrivingArmedStateService = drivingArmedStateService ?? throw new ArgumentNullException(nameof(drivingArmedStateService));
        MockPhprOutput = mockPhprOutput ?? throw new ArgumentNullException(nameof(mockPhprOutput));
        PhprValidationExporter = phprValidationExporter ?? throw new ArgumentNullException(nameof(phprValidationExporter));
        SupportBundleExporter = supportBundleExporter ?? throw new ArgumentNullException(nameof(supportBundleExporter));
        SelectedRecordingDetailExporter = selectedRecordingDetailExporter ?? throw new ArgumentNullException(nameof(selectedRecordingDetailExporter));
        PhprHidOpenCheckRunner = phprHidOpenCheckRunner ?? throw new ArgumentNullException(nameof(phprHidOpenCheckRunner));
        PaddleGearBenchTestController = paddleGearBenchTestController ?? throw new ArgumentNullException(nameof(paddleGearBenchTestController));
        PhprSoftwareCoexistenceDetector = phprSoftwareCoexistenceDetector ?? throw new ArgumentNullException(nameof(phprSoftwareCoexistenceDetector));
        PhprDirectOutputCandidateProvider = phprDirectOutputCandidateProvider ?? throw new ArgumentNullException(nameof(phprDirectOutputCandidateProvider));
        ProfileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        PhprProfileStore = phprProfileStore ?? throw new ArgumentNullException(nameof(phprProfileStore));
        RuntimeLifecycleCoordinator = runtimeLifecycleCoordinator ?? throw new ArgumentNullException(nameof(runtimeLifecycleCoordinator));
    }

    public EffectSettingsListViewModel EffectSettingsViewModel { get; }

    public SafetyStateViewModel SafetyStateViewModel { get; }

    public TelemetryStatusViewModel TelemetryStatusViewModel { get; }

    public OutputDeviceStatusViewModel OutputDeviceStatusViewModel { get; }

    public RecordingReplayStatusViewModel RecordingReplayStatusViewModel { get; }

    public PhprStatusViewModel PhprStatusViewModel { get; }

    public DiagnosticsSummaryViewModel DiagnosticsSummaryViewModel { get; }

    public ApplicationSafetyController ApplicationSafetyController { get; }

    public TelemetrySessionController TelemetrySessionController { get; }

    public AudioOutputController AudioOutputController { get; }

    public RecordingReplayController RecordingReplayController { get; }

    public PhprOutputController PhprOutputController { get; }

    public DiagnosticsPresentationController DiagnosticsPresentationController { get; }

    public AppSettingsStore SettingsStore { get; }

    public AppSettingsHydrationSnapshot SettingsHydrationSnapshot { get; }

    public IAsioDriverCatalog AsioDriverCatalog { get; }

    public AsioDriverVisibilityDiagnostics AsioVisibilityDiagnostics { get; }

    public AsioReadinessDiagnostics AsioReadinessDiagnostics { get; }

    public IOutputInterlock OutputInterlock { get; }

    public IPHprWriteAuthorization PhprWriteAuthorization { get; }

    public AudioTestBench TestBench { get; }

    public IInputDeviceDiscovery InputDeviceDiscovery { get; }

    public IWheelInputCandidateProvider WheelInputCandidateProvider { get; }

    public IWheelPaddleInputSource PaddleInputSource { get; }

    public DrivingArmedStateService DrivingArmedStateService { get; }

    public MockPhprOutputDevice MockPhprOutput { get; }

    public PHprManualValidationResultExporter PhprValidationExporter { get; }

    public SupportBundleExporter SupportBundleExporter { get; }

    public SelectedRecordingDetailExporter SelectedRecordingDetailExporter { get; }

    public PHprHidOpenCheckRunner PhprHidOpenCheckRunner { get; }

    public PaddleGearBenchTestController PaddleGearBenchTestController { get; }

    public IPHprSoftwareCoexistenceDetector PhprSoftwareCoexistenceDetector { get; }

    public IPHprDirectOutputCandidateProvider PhprDirectOutputCandidateProvider { get; }

    public HapticProfileStore ProfileStore { get; }

    public PhprEffectProfileStore PhprProfileStore { get; }

    public RuntimeLifecycleCoordinator RuntimeLifecycleCoordinator { get; }
}
