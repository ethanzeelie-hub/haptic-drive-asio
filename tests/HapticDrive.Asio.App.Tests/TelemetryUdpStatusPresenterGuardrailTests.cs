using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class TelemetryUdpStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "TelemetryUdpStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeAsioOutputBackend", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayService.StopAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordingService.StartAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesTelemetryUdpPresenterAndViewBoundary()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("TelemetryUdpViewControl.Apply(BuildTelemetryUdpStatusPresentation());", source, StringComparison.Ordinal);
        Assert.Contains("TelemetryUdpStatusPresenter.Build(new TelemetryUdpStatusSnapshot(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordingsDetailText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayDetailText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ForwardingDestinationsSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayTimingModeHelpText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordingsStartStopButton.Content =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayStartStopButton.Content =", source, StringComparison.Ordinal);
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
