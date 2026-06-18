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
    public void MainWindowSource_StillOwnsResidualExecutionHeavyEntryPoints()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("private async void StartStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("? await _hapticPipeline.StopAsync()", source, StringComparison.Ordinal);
        Assert.Contains(": await _hapticPipeline.StartAsync();", source, StringComparison.Ordinal);
        Assert.Contains("private async void EmergencyMuteButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("var pipelineMuteResult = await _hapticPipeline.SetEmergencyMuteAsync(_emergencyMuted);", source, StringComparison.Ordinal);
        Assert.Contains("_testBench.EmergencyMute = _emergencyMuted;", source, StringComparison.Ordinal);
        Assert.Contains("private async void PhprPedalsStopAllClearDeviceStateButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.StopAllAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.EmergencyStopAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.InitializeStartupCleanupAsync();", source, StringComparison.Ordinal);
        Assert.Contains("await _telemetryReceiver.StartAsync();", source, StringComparison.Ordinal);
        Assert.Contains("_telemetryStatusTimer.Start();", source, StringComparison.Ordinal);
        Assert.Contains("var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();", source, StringComparison.Ordinal);
        Assert.Contains("_hapticPipeline.StopManualAsioHardwareTest(", source, StringComparison.Ordinal);
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
