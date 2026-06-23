using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class ShutdownCleanupPlannerGuardrailTests
{
    [Fact]
    public void PlannerSource_HasNoWpfOrOutputStartExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "ShutdownCleanupPlanner.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Button", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Window", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitBufferAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAll", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSession_UsesShutdownPlannerWithoutPlannerExecutingCleanup()
    {
        var runtimeSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppRuntimeSession.cs");

        Assert.Contains("var plan = ShutdownCleanupPlanner.BuildAppShutdownPlan();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("switch (step.Kind)", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _realPhprContinuousEffectsRuntime", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_hapticPipeline.StopManualAsioHardwareTest(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _realPhprOutput.DisposeAsync()", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("await _hapticPipeline.DisposeAsync()", runtimeSource, StringComparison.Ordinal);
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
