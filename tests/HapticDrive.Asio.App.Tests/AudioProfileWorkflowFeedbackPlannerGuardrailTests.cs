using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class AudioProfileWorkflowFeedbackPlannerGuardrailTests
{
    [Fact]
    public void PlannerSource_HasNoWpfOrRuntimeExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "AudioProfileWorkflowFeedbackPlanner.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Slider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hapticPipeline", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_testBench", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesFeedbackPlannerForAudioProfileWorkflowMessages()
    {
        var mainWindowSource = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();
        var controllerSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "Controllers",
            "ProfileTuningController.cs");

        Assert.Contains("AudioProfileWorkflowFeedbackPlanner.BuildSaveProfilesFeedback(", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("AudioProfileWorkflowFeedbackPlanner.BuildLoadProfilesFeedback(", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("AudioProfileWorkflowFeedbackPlanner.BuildResetFeedback(", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("AudioProfileWorkflowFeedbackPlanner.BuildTuningChangedFeedback(", controllerSource, StringComparison.Ordinal);
        Assert.Contains("AudioProfileWorkflowFeedbackPlanner.BuildProfileNameCommitFeedback(", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FooterStatusText.Text = \"Tuning applied to the output-owned render path.\"", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FooterStatusText.Text = \"Tuning applied; haptics are still stopped.\"", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FooterStatusText.Text = \"Reset tuning to the current rig defaults.\"", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FooterStatusText.Text = \"Reset tuning to the current rig defaults for the output-owned render path.\"", mainWindowSource, StringComparison.Ordinal);
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
