using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class GeminiReviewClosureGuardrailTests
{
    [Fact]
    public void FinalStage21HelperSet_RemainsPureOfWpfAndExecutionOwnership()
    {
        var repositoryRoot = FindRepositoryRoot();
        var helperFiles = new[]
        {
            "DashboardStatusPresenter.cs",
            "DevicesStatusPresenter.cs",
            "EffectsStatusPresenter.cs",
            "RoutingMixerStatusPresenter.cs",
            "TelemetryUdpStatusPresenter.cs",
            "ProfilesStatusPresenter.cs",
            "TestingValidationStatusPresenter.cs",
            "PhprWorkflowStatusPresenter.cs",
            "DiagnosticsStatusPresenter.cs",
            "AppSettingsSnapshotBuilder.cs",
            "ControlSettingsSnapshotBuilder.cs",
            "AudioProfileControlSnapshotBuilder.cs",
            "LocalGearReadinessPresenter.cs",
            "StartupReadinessPlanner.cs",
            "ShutdownCleanupPlanner.cs",
            "SafetyContextSnapshotBuilder.cs",
            "HapticsControlStatePresenter.cs"
        };

        foreach (var fileName in helperFiles)
        {
            var source = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "HapticDrive.Asio.App",
                fileName));

            Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SubmitBufferAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SetEmergencyMuteAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EmergencyStopAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StopAllAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplyPaddleGearBenchRuntimeBlockToControls", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MainWindowShell_StaysThinWhileRuntimeSessionOwnsExecutionHeavyEntryPoints()
    {
        var mainWindowSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs");
        var runtimeSource = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("_runtime = new AppRuntimeSession(this, services);", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_runtime.MainWindow_PreviewKeyDown(sender, e);", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_outputInterlock.Trip(", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_hapticPipeline.StartAsync()", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_hapticPipeline.StopAsync()", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_phprDirectRuntime.StopAllAsync(", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_telemetryReceiver.StartAsync()", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StartStopButton_Click(", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetOutputInterlockButton_Click(", mainWindowSource, StringComparison.Ordinal);

        Assert.Contains("internal async void StartStopButton_Click", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("? await _hapticPipeline.StopAsync()", runtimeSource, StringComparison.Ordinal);
        Assert.Contains(": await _hapticPipeline.StartAsync();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("internal async void EmergencyMuteButton_Click", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock.Trip(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("internal async void ResetOutputInterlockButton_Click", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("internal async void MainWindow_PreviewKeyDown", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("private async void PhprPedalsStopAllClearDeviceStateButton_Click", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.StopAllAsync(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.EmergencyStopAsync(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.InitializeStartupCleanupAsync();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _telemetryReceiver.StartAsync();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_telemetryStatusTimer.Start();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock.Trip(OutputInterlockReason.Shutdown", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_hapticPipeline.StopManualAsioHardwareTest(", runtimeSource, StringComparison.Ordinal);
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
