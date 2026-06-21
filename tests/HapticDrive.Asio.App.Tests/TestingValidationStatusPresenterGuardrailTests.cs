using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class TestingValidationStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "TestingValidationStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeAsioOutputBackend", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StartManualAsioHardwareTestAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportMarkdown", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesTestingValidationPresenterAndViewBoundary()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("TestingValidationViewControl.Apply(BuildTestingValidationStatusPresentation());", source, StringComparison.Ordinal);
        Assert.Contains("TestingValidationStatusPresenter.Build(new TestingValidationStatusSnapshot(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TestBenchStartStopButton.Content =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TestBenchStateText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TestBenchPeakText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TestBenchLimiterText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TestBenchOutputText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TestBenchWarningText.Text =", source, StringComparison.Ordinal);
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