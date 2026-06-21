using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class StartupReadinessPlannerGuardrailTests
{
    [Fact]
    public void PlannerSource_HasNoWpfOrOutputRuntimeReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "StartupReadinessPlanner.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PHprHidOpenCheckRunner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAll", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesStartupPlannerForExtractedStartupReadinessPlanning()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("StartupReadinessPlanner.BuildAsioSelectionPlan(", source, StringComparison.Ordinal);
        Assert.Contains("StartupReadinessPlanner.BuildStartupPhprAutoReadySelection(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("var selection = Bst1AsioStartupDefaults.Resolve(_asioVisibilitySnapshot.DriverNames);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhprDirectAutoReadySelector.Select(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("enableWhenPreferredPresent: true", source, StringComparison.Ordinal);
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