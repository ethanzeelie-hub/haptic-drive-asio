using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class ControlSettingsSnapshotBuilderGuardrailTests
{
    [Fact]
    public void BuilderSource_HasNoWpfOrHardwareRuntimeReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "ControlSettingsSnapshotBuilder.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesControlSettingsBuilderInsteadOfOwningMovedParsingBlocks()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("ControlSettingsSnapshotBuilder.TryBuildPaddleMapping(", source, StringComparison.Ordinal);
        Assert.Contains("ControlSettingsSnapshotBuilder.TryBuildMockGearPulseOptions(", source, StringComparison.Ordinal);
        Assert.Contains("ControlSettingsSnapshotBuilder.TryBuildRealPhprOutputOptions(", source, StringComparison.Ordinal);
        Assert.Contains("ControlSettingsSnapshotBuilder.TryBuildBst1ManualPulseSettings(", source, StringComparison.Ordinal);
        Assert.Contains("ControlSettingsSnapshotBuilder.BuildRealPhprControlValues(", source, StringComparison.Ordinal);
        Assert.Contains("ControlSettingsSnapshotBuilder.BuildBst1PulseControlValues(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParsePercent(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseOptionalButtonId(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryParseOptionalReportId(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void ApplyRealGearPulseSettingsToControls(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"BST-1 strength must be a number from 0 to 100%.\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Real P-HPR report length must be a whole number from 1 to 1024 bytes.\"", source, StringComparison.Ordinal);
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