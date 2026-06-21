using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class AppSettingsSnapshotBuilderGuardrailTests
{
    [Fact]
    public void BuilderSource_HasNoWpfControlOrHardwareStartStopReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "AppSettingsSnapshotBuilder.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsControl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesSettingsBuilderAndPresenterInsteadOfOwningSaveAndStatusShaping()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("AppSettingsSnapshotBuilder.BuildHydrationSnapshot(_settingsStore.Load())", source, StringComparison.Ordinal);
        Assert.Contains("AppSettingsSnapshotBuilder.BuildAppSettings(BuildCurrentAppSettingsSaveInputs())", source, StringComparison.Ordinal);
        Assert.Contains("PersistedSettingsStatusPresenter.Build(new PersistedSettingsStatusSnapshot(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_settingsStore.Save(new AppSettings", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"App settings path: \"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manual ASIO test active state are not saved", source, StringComparison.Ordinal);
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