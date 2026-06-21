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
    public void MainWindowSource_KeepsCompositionStartupAndSafetyExecutionInline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("_phprDirectRuntime = new PHprDirectRuntimeCoordinator(", source, StringComparison.Ordinal);
        Assert.Contains("_realPhprContinuousEffectsRuntime = new PHprContinuousEffectsRuntimeCoordinator(", source, StringComparison.Ordinal);
        Assert.Contains("_paddleInputRoutingCoordinator = new PaddleInputRoutingCoordinator(", source, StringComparison.Ordinal);
        Assert.Contains("DevicesViewControl.PhprPedalsEmergencyStopClicked += PhprPedalsEmergencyStopButton_Click;", source, StringComparison.Ordinal);
        Assert.Contains("DevicesViewControl.PhprPedalsStopAllClearDeviceStateClicked += PhprPedalsStopAllClearDeviceStateButton_Click;", source, StringComparison.Ordinal);
        Assert.Contains("AdvancedDiagnosticsViewControl.RealPhprEmergencyStopClicked += RealPhprEmergencyStopButton_Click;", source, StringComparison.Ordinal);
        Assert.Contains("TestingValidationViewControl.TestBenchStartStopClicked += TestBenchStartStopButton_Click;", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.InitializeStartupCleanupAsync();", source, StringComparison.Ordinal);
        Assert.Contains("await _telemetryReceiver.StartAsync();", source, StringComparison.Ordinal);
        Assert.Contains("_realPhprContinuousEffectsRuntime.StartSlipLockRuntime();", source, StringComparison.Ordinal);
        Assert.Contains("_realPhprContinuousEffectsRuntime.StartRoadVibrationRuntime();", source, StringComparison.Ordinal);
        Assert.Contains("private async void StartStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("? await _hapticPipeline.StopAsync()", source, StringComparison.Ordinal);
        Assert.Contains(": await _hapticPipeline.StartAsync();", source, StringComparison.Ordinal);
        Assert.Contains("private async void EmergencyMuteButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock.Trip(", source, StringComparison.Ordinal);
        Assert.Contains("private async void ResetOutputInterlockButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("private async void MainWindow_PreviewKeyDown", source, StringComparison.Ordinal);
        Assert.Contains("private async void PhprPedalsEmergencyStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.EmergencyStopAsync(\"normal P-HPR pedals emergency stop button\");", source, StringComparison.Ordinal);
        Assert.Contains("private async void PhprPedalsStopAllClearDeviceStateButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("var result = await _phprDirectRuntime.StopAllAsync(\"manual P-HPR Stop All / Clear Device State button\");", source, StringComparison.Ordinal);
        Assert.Contains("var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();", source, StringComparison.Ordinal);
        Assert.Contains("case ShutdownCleanupStepKind.StopContinuousPhprRuntime:", source, StringComparison.Ordinal);
        Assert.Contains("await _realPhprContinuousEffectsRuntime", source, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock.Trip(OutputInterlockReason.Shutdown", source, StringComparison.Ordinal);
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
