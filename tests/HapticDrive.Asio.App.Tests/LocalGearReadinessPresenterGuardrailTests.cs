using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class LocalGearReadinessPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareWriteReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "LocalGearReadinessPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesLocalGearPresenterInsteadOfOwningInlineReadinessStrings()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("LocalGearReadinessPresenter.Build(readiness, _localGearTestAutoStartListener)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Start Haptics required: NO; F1 telemetry required: NO; live telemetry effects started: NO.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Start the read-only paddle listener for Local Gear Test Mode without Start Haptics or F1 telemetry.", source, StringComparison.Ordinal);
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
