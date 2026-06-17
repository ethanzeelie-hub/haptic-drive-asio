using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprWorkflowStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareWriteReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "PhprWorkflowStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsControl", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesWorkflowPresenterInsteadOfOwningStatusStrings()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("PhprWorkflowStatusSnapshotBuilder.Build(new PhprWorkflowStatusBuildInputs(", source, StringComparison.Ordinal);
        Assert.Contains("PhprWorkflowStatusPresenter.Build(snapshot)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"P-HPR mode:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Replay validation:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Instant gear pulse:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Real output counters:", source, StringComparison.Ordinal);
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
