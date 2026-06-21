using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class DashboardStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "DashboardStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Window", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeAsioOutputBackend", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetEmergencyMuteAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStopAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesDashboardPresenterAndViewBoundary()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("var presentation = BuildDashboardStatusPresentation(pipelineSnapshot);", source, StringComparison.Ordinal);
        Assert.Contains("DashboardViewControl.Apply(presentation);", source, StringComparison.Ordinal);
        Assert.Contains("DashboardStatusPresenter.Build(new DashboardStatusSnapshot(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputModeValueText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UdpListenerValueText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ForwardingValueText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HeaderParserValueText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VehicleStateValueText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordingValueText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DashboardWorkflowStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DashboardNextStepText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DashboardChecklistItemsControl.ItemsSource =", source, StringComparison.Ordinal);
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