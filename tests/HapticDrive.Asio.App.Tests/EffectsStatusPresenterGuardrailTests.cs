using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class EffectsStatusPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "EffectsStatusPresenter.cs"));

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
    public void MainWindowSource_UsesEffectsPresenterAndViewBoundary()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("EffectsViewControl.Apply(presentation);", source, StringComparison.Ordinal);
        Assert.Contains("EffectsStatusPresenter.Build(", source, StringComparison.Ordinal);
        Assert.Contains("EffectsStatusSnapshotBuilder.Build(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedRoadSignalStatusText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineEffectStateText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineEffectDetailText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GearShiftEffectStateText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadTextureEffectStateText.Text =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SlipEffectStateText.Text =", source, StringComparison.Ordinal);
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