using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class HapticsControlStatePresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfOrExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "HapticsControlStatePresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBlock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Window", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SubmitBufferAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetEmergencyMuteAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStopAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStartupCleanupAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyPaddleGearBenchRuntimeBlockToControls", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlEnabled =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSession_UsesPresenterWithoutGivingPresenterRuntimeOwnership()
    {
        var runtimeSource = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("HapticsControlStatePresenter.Build(new HapticsControlStateSnapshot(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("? await _hapticPipeline.StopAsync()", runtimeSource, StringComparison.Ordinal);
        Assert.Contains(": await _hapticPipeline.StartAsync();", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("var pipelineMuteResult = await _hapticPipeline.SetEmergencyMuteAsync(_emergencyMuted);", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StartStopButton.Content = _hapticsStarted ? \"Stop Haptics\" : \"Start Haptics\";", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyMuteButton.Content = _emergencyMuted ? \"Clear Mute\" : \"Emergency Mute\";", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HapticsStateText.Text = \"Emergency muted\";", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HapticsStateText.Text = \"Telemetry stale mute\";", runtimeSource, StringComparison.Ordinal);
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
