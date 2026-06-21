using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class AudioProfileViewSyncCoordinatorGuardrailTests
{
    [Fact]
    public void CoordinatorSource_HasNoWpfOrRuntimeExecutionReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "AudioProfileViewSyncCoordinator.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UserControl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hapticPipeline", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_testBench", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesViewSyncCoordinatorForAudioProfileViewOperations()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("AudioProfileViewSyncCoordinator.BuildCurrentControlInputs(", source, StringComparison.Ordinal);
        Assert.Contains("AudioProfileViewSyncCoordinator.ApplyControlValues(", source, StringComparison.Ordinal);
        Assert.Contains("AudioProfileViewSyncCoordinator.ApplyControlText(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfilesViewControl.BuildAudioProfileNameInput(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectsViewControl.BuildAudioProfileEffectControlInputs(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoutingMixerViewControl.BuildAudioProfileMixerControlInputs(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfilesViewControl.ApplyAudioProfileControlValues(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectsViewControl.ApplyAudioProfileEffectControlValues(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoutingMixerViewControl.ApplyAudioProfileMixerControlValues(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectsViewControl.ApplyAudioProfileEffectControlText(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoutingMixerViewControl.ApplyAudioProfileMixerControlText(", source, StringComparison.Ordinal);
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