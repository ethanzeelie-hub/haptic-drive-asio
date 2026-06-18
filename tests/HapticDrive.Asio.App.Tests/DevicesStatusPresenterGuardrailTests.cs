using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class DevicesStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "DevicesStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
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
    public void MainWindowSource_UsesDevicesPresenterAndViewBoundary()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("DevicesViewControl.Apply(presentation);", source, StringComparison.Ordinal);
        Assert.Contains("DevicesStatusPresenter.Build(new DevicesStatusSnapshot(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentOutputStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NullOutputStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WasapiDebugStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioReadinessStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HardwareChainStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TrueAsioStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PaddleInputBadgeText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShiftIntentStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShiftIntentItemsControl.ItemsSource =", source, StringComparison.Ordinal);
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
