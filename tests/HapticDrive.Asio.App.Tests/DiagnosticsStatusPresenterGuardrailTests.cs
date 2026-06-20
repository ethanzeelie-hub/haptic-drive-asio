using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class DiagnosticsStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareWriteReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "DiagnosticsStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsControl", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesDiagnosticsPresenterInsteadOfOwningClipboardAndWorkflowDiagnosticsStrings()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("DiagnosticsStatusSnapshotBuilder.Build(new DiagnosticsStatusBuildInputs(", source, StringComparison.Ordinal);
        Assert.Contains("Bst1DiagnosticsSectionBuilder.Build(new Bst1DiagnosticsSectionInputs(", source, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsStatusPresenter.Build(snapshot)", source, StringComparison.Ordinal);
        Assert.Contains("BuildPhprWorkflowStatusPresentation(pipelineSnapshot, realDiagnostics)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Haptic Drive ASIO diagnostics\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhprWorkflowDiagnosticsReport.BuildProfilePersistenceLine(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhprWorkflowDiagnosticsReport.BuildWorkflowLine(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhprLiveF1ValidationGuide.Build(BuildPhprLiveF1ValidationSnapshot(", source, StringComparison.Ordinal);
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
