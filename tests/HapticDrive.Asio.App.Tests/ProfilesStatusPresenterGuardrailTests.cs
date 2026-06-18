using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class ProfilesStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "ProfilesStatusPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeAsioOutputBackend", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_profileStore.SaveAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_profileStore.LoadAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_phprProfileStore.SaveAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_phprProfileStore.LoadAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesProfilesPresenterAndViewBoundary()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("ProfilesViewControl.Apply(BuildProfilesStatusPresentation(message, validationMessages));", source, StringComparison.Ordinal);
        Assert.Contains("ProfilesStatusPresenter.Build(new ProfilesStatusSnapshot(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfilePathText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfilePhprStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileValidationText.Text =", source, StringComparison.Ordinal);
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
