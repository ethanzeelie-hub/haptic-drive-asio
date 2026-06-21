using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowSafetyTests
{
    [Fact]
    public void EmergencyMuteTripsGlobalInterlock()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("_outputInterlock.Trip(", source, StringComparison.Ordinal);
        Assert.Contains("OutputInterlockReason.UserEmergencyMute", source, StringComparison.Ordinal);
        Assert.Contains("private async void ResetOutputInterlockButton_Click", source, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyboardShortcutsAndShutdownUseGlobalInterlock()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("if (e.Key == Key.M)", source, StringComparison.Ordinal);
        Assert.Contains("if (e.Key == Key.R)", source, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock.Trip(OutputInterlockReason.Shutdown", source, StringComparison.Ordinal);
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