using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class SafetyContextSnapshotBuilderGuardrailTests
{
    [Fact]
    public void BuilderSource_HasNoWpfHardwareOrLifecycleExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "SafetyContextSnapshotBuilder.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Button", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Window", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitBufferAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStopAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlEnabled =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesSafetyContextBuilderButKeepsExecutionAndMutationOwnership()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("SafetyContextSnapshotBuilder.BuildMockRuntimeSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("SafetyContextSnapshotBuilder.BuildManualRealSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("SafetyContextSnapshotBuilder.BuildBenchMockSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("SafetyContextSnapshotBuilder.BuildBenchDirectSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.EmergencyStopAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.StopAllAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await _phprDirectRuntime.InitializeStartupCleanupAsync();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("return new PHprSafetyContext(", source, StringComparison.Ordinal);
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
