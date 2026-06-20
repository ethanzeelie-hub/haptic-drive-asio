using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class RoutingMixerStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "RoutingMixerStatusPresenter.cs"));

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
    public void MainWindowSource_UsesRoutingMixerPresenterAndViewBoundary()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("RoutingMixerViewControl.Apply(presentation);", source, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerStatusPresenter.Build(", source, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerStatusSnapshotBuilder.Build(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MixerEmergencyMuteStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MixerOutputPeakStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MixerLimiterActivityStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Bst1RoutingSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Bst1EffectsSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BrakePhprRoutingSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BrakePhprEffectsSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ThrottlePhprRoutingSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ThrottlePhprEffectsSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveEffectsSummaryText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PriorityDuckingSummaryText.Text =", source, StringComparison.Ordinal);
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
