using System.IO;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class GeminiRuntimeStartOwnershipAuditGuardrailTests
{
    [Fact]
    public void ExtractedViewCodeBehind_RemainsEventForwardingOnly()
    {
        var viewsDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "Views");

        foreach (var path in Directory.GetFiles(viewsDirectory, "*.xaml.cs"))
        {
            var source = File.ReadAllText(path);

            Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SetEmergencyMuteAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EmergencyStopAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StopAllAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PHprDirectRuntimeCoordinator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PHprContinuousEffectsRuntimeCoordinator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HapticPipelineCoordinator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompositionRoot_SeparatesAppStartupFromShellConstruction()
    {
        var appSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "App.xaml.cs");
        var rootSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppCompositionRoot.cs");
        var servicesSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppServices.cs");
        var mainWindowSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs");
        var runtimeSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppRuntimeSession.cs");

        Assert.Contains("_compositionRoot = new AppCompositionRoot();", appSource, StringComparison.Ordinal);
        Assert.Contains("MainWindow = _compositionRoot.CreateMainWindow();", appSource, StringComparison.Ordinal);
        Assert.Contains("Services = new AppServices(", rootSource, StringComparison.Ordinal);
        Assert.Contains("return new MainWindow(Services);", rootSource, StringComparison.Ordinal);
        Assert.Contains("internal MainWindow(AppServices services)", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_runtime = new AppRuntimeSession(this, services);", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.DataContext = _effectSettingsViewModel;", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_applicationSafetyController = services.ApplicationSafetyController;", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_telemetrySessionController = services.TelemetrySessionController;", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_audioOutputController = services.AudioOutputController;", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_recordingReplayController = services.RecordingReplayController;", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_phprOutputController = services.PhprOutputController;", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_diagnosticsPresentationController = services.DiagnosticsPresentationController;", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_applicationSafetyController = services.ApplicationSafetyController;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_telemetrySessionController = services.TelemetrySessionController;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_audioOutputController = services.AudioOutputController;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_recordingReplayController = services.RecordingReplayController;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_phprOutputController = services.PhprOutputController;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_diagnosticsPresentationController = services.DiagnosticsPresentationController;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_settingsStore = services.SettingsStore;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock = services.OutputInterlock;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_phprWriteAuthorization = services.PhprWriteAuthorization;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_testBench = services.TestBench;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("var appSettings = services.SettingsHydrationSnapshot;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("public ApplicationSafetyController ApplicationSafetyController { get; }", servicesSource, StringComparison.Ordinal);
        Assert.Contains("public TelemetrySessionController TelemetrySessionController { get; }", servicesSource, StringComparison.Ordinal);
        Assert.Contains("public AppSettingsStore SettingsStore { get; }", servicesSource, StringComparison.Ordinal);
        Assert.Contains("public IOutputInterlock OutputInterlock { get; }", servicesSource, StringComparison.Ordinal);
        Assert.Contains("public RuntimeLifecycleCoordinator RuntimeLifecycleCoordinator { get; }", servicesSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuousEffectsRuntimeCoordinator_OwnsBackgroundLoopBodiesOutsideApp()
    {
        Assert.Equal("HapticDrive.Actuation", typeof(PHprContinuousEffectsRuntimeCoordinator).Assembly.GetName().Name);

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Actuation",
            "PHpr",
            "PHprContinuousEffectsRuntimeCoordinator.cs"));

        Assert.Contains("public void StartSlipLockRuntime()", source, StringComparison.Ordinal);
        Assert.Contains("public void StartRoadVibrationRuntime()", source, StringComparison.Ordinal);
        Assert.Contains("private async Task RunRealSlipLockRuntimeAsync(", source, StringComparison.Ordinal);
        Assert.Contains("private async Task RunRealRoadVibrationRuntimeAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await _clock.DelayAsync(RuntimeCadence, cancellationToken).ConfigureAwait(false);", source, StringComparison.Ordinal);
        Assert.Contains("await _slipLockRouter.StopIfHoldExpiredAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await _roadVibrationRouter.StopIfHoldExpiredAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PaddleRoutingAndDirectRuntime_RemainSeparatedFromViewAndShellExecution()
    {
        Assert.Equal("HapticDrive.Asio.App", typeof(PaddleInputRoutingCoordinator).Assembly.GetName().Name);
        Assert.Equal("HapticDrive.Simagic.PHPR.Output.Windows", typeof(PHprDirectRuntimeCoordinator).Assembly.GetName().Name);
        Assert.Equal("HapticDrive.Asio.Runtime", typeof(HapticPipelineCoordinator).Assembly.GetName().Name);

        var repositoryRoot = FindRepositoryRoot();
        var routingSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HapticDrive.Asio.App",
            "PaddleInputRoutingCoordinator.cs"));
        var directRuntimeSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HapticDrive.Simagic.PHPR.Output.Windows",
            "PHprDirectRuntime.cs"));

        Assert.Contains("public async ValueTask<PaddleInputRoutingHandleResult> HandleAsync(", routingSource, StringComparison.Ordinal);
        Assert.Contains("private async Task<PaddleGearBenchRoutingResult> RoutePaddleGearBenchAsync(", routingSource, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.RouteBenchAsync(", routingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", routingSource, StringComparison.Ordinal);
        Assert.Contains("ValueTask InitializeStartupCleanupAsync(", directRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ValueTask<PHprDirectStopAllResult> StopAllAsync(", directRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("ValueTask EmergencyStopAsync(", directRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow", directRuntimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupAndShutdownPlanners_RemainPurePlanningHelpers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var startupSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HapticDrive.Asio.App",
            "StartupReadinessPlanner.cs"));
        var shutdownSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HapticDrive.Asio.App",
            "ShutdownCleanupPlanner.cs"));

        Assert.Contains("BuildAsioSelectionPlan", startupSource, StringComparison.Ordinal);
        Assert.Contains("BuildStartupPhprAutoReadySelection", startupSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", startupSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", startupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", startupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAll", startupSource, StringComparison.Ordinal);

        Assert.Contains("BuildAppShutdownPlan", shutdownSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", shutdownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetEmergencyMuteAsync", shutdownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", shutdownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAll", shutdownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStartupCleanupAsync", shutdownSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "HapticDrive.Asio.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing HapticDrive.Asio.sln.");
    }
}
