using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Driving;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime;
using HapticDrive.Asio.Runtime.Diagnostics;
using HapticDrive.Asio.Runtime.Safety;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Windows;
using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Readiness;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Validation;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed class AppCompositionRoot
{
    public AppCompositionRoot()
    {
        var effectSettingsViewModel = new EffectSettingsListViewModel();
        var safetyStateViewModel = new SafetyStateViewModel();
        var telemetryStatusViewModel = new TelemetryStatusViewModel();
        var outputDeviceStatusViewModel = new OutputDeviceStatusViewModel();
        var recordingReplayStatusViewModel = new RecordingReplayStatusViewModel();
        var phprStatusViewModel = new PhprStatusViewModel();
        var diagnosticsSummaryViewModel = new DiagnosticsSummaryViewModel();
        var settingsStore = new AppSettingsStore();
        var settingsHydrationSnapshot = AppSettingsSnapshotBuilder.BuildHydrationSnapshot(settingsStore.Load());
        var asioDriverCatalog = new WindowsRegistryAsioDriverCatalog();
        var outputInterlock = new OutputInterlock();
        var phprWriteAuthorization = new PHprSessionWriteAuthorization();
        var testBench = new AudioTestBench();
        var drivingArmedStateService = new DrivingArmedStateService();
        var diagnosticSink = new InMemoryDiagnosticSink();
        var diagnosticCorrelationContext = new DiagnosticCorrelationContext();
        var runtimeHealthMonitor = new RuntimeHealthMonitor(diagnosticSink, diagnosticCorrelationContext);

        Services = new AppServices(
            effectSettingsViewModel,
            safetyStateViewModel,
            telemetryStatusViewModel,
            outputDeviceStatusViewModel,
            recordingReplayStatusViewModel,
            phprStatusViewModel,
            diagnosticsSummaryViewModel,
            new ApplicationSafetyController(safetyStateViewModel),
            new TelemetrySessionController(telemetryStatusViewModel),
            new AudioOutputController(outputDeviceStatusViewModel),
            new RecordingReplayController(recordingReplayStatusViewModel),
            new PhprOutputController(phprStatusViewModel),
            new DiagnosticsPresentationController(diagnosticsSummaryViewModel),
            settingsStore,
            settingsHydrationSnapshot,
            asioDriverCatalog,
            new AsioDriverVisibilityDiagnostics(asioDriverCatalog),
            new AsioReadinessDiagnostics(asioDriverCatalog),
            outputInterlock,
            phprWriteAuthorization,
            diagnosticSink,
            diagnosticCorrelationContext,
            runtimeHealthMonitor,
            testBench,
            new WindowsInputDeviceDiscovery(),
            new WheelInputCandidateProvider(),
            new PollingWheelPaddleInputSource(new WindowsGameControllerButtonStateReader()),
            drivingArmedStateService,
            new MockPhprOutputDevice(),
            new PHprManualValidationResultExporter(),
            new SupportBundleExporter(),
            new SelectedRecordingDetailExporter(),
            new PHprHidOpenCheckRunner(),
            new PaddleGearBenchTestController(PaddleGearBenchTestOptions.EnabledDirect),
            new PHprSoftwareCoexistenceDetector(new WindowsProcessSnapshotProvider()),
            new WindowsPhprDirectOutputCandidateProvider(),
            new HapticProfileStore(),
            new PhprEffectProfileStore(),
            new RuntimeLifecycleCoordinator());
    }

    public AppServices Services { get; }

    public MainWindow CreateMainWindow()
    {
        return new MainWindow(Services);
    }
}
