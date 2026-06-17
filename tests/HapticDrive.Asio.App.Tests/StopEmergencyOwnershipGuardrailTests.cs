using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class StopEmergencyOwnershipGuardrailTests
{
    [Fact]
    public void MainWindowSource_KeepsStopAllEmergencyStopAndRecoveryExecutionInline()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("private async void MockGearPulseEmergencyStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("await _mockGearPulseRouter.EmergencyStopAsync();", source, StringComparison.Ordinal);
        Assert.Contains("_mockGearPulseRouter.ClearEmergencyStop();", source, StringComparison.Ordinal);
        Assert.Contains("private async void MockPedalEffectsEmergencyStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("await _mockPedalEffectsRouter.EmergencyStopAsync();", source, StringComparison.Ordinal);
        Assert.Contains("_mockPedalEffectsRouter.ClearEmergencyStop();", source, StringComparison.Ordinal);
        Assert.Contains("private async void RealPhprEmergencyStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.EmergencyStopAsync(\"advanced direct-control emergency stop button\");", source, StringComparison.Ordinal);
        Assert.Contains("private async void PhprPedalsEmergencyStopButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.EmergencyStopAsync(\"normal P-HPR pedals emergency stop button\");", source, StringComparison.Ordinal);
        Assert.Contains("private async void PhprPedalsStopAllClearDeviceStateButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("var result = await _phprDirectRuntime.StopAllAsync(\"manual P-HPR Stop All / Clear Device State button\");", source, StringComparison.Ordinal);
        Assert.Contains("ApplyPaddleGearBenchRuntimeBlockToControls();", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.InitializeStartupCleanupAsync();", source, StringComparison.Ordinal);
        Assert.Contains("var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractedAppPlannersAndBuilders_DoNotAbsorbStopEmergencyExecution()
    {
        var repositoryRoot = FindRepositoryRoot();
        var extractedSources = new[]
        {
            Path.Combine(repositoryRoot, "src", "HapticDrive.Asio.App", "HapticsControlStatePresenter.cs"),
            Path.Combine(repositoryRoot, "src", "HapticDrive.Asio.App", "StartupReadinessPlanner.cs"),
            Path.Combine(repositoryRoot, "src", "HapticDrive.Asio.App", "ShutdownCleanupPlanner.cs"),
            Path.Combine(repositoryRoot, "src", "HapticDrive.Asio.App", "SafetyContextSnapshotBuilder.cs")
        };

        foreach (var path in extractedSources)
        {
            var source = File.ReadAllText(path);

            Assert.DoesNotContain("EmergencyStopAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StopAllAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ClearEmergencyStop", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SetEmergencyMuteAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_hapticPipeline.StartAsync()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_hapticPipeline.StopAsync()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplyPaddleGearBenchRuntimeBlockToControls", source, StringComparison.Ordinal);
            Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
        }
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
