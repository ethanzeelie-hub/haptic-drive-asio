using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class StartupOutputInterlockPlannerGuardrailTests
{
    [Fact]
    public void PlannerSource_HasNoWpfOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "StartupOutputInterlockPlanner.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesStartupOutputInterlockPlanner()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("StartupOutputInterlockPlanner.Build(new StartupOutputInterlockSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("await ApplyStartupOutputInterlockPlanAsync();", source, StringComparison.Ordinal);
    }
}
