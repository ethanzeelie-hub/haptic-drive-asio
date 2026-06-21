using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class AudioProfileControlSnapshotBuilderGuardrailTests
{
    [Fact]
    public void BuilderSource_HasNoWpfControlOrHardwareStartStopReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "AudioProfileControlSnapshotBuilder.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Slider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EmergencyStop", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesAudioProfileBuilderInsteadOfOwningInlineProfileMapping()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("AudioProfileControlSnapshotBuilder.BuildProfile(", source, StringComparison.Ordinal);
        Assert.Contains("AudioProfileControlSnapshotBuilder.BuildApplicationPlan(", source, StringComparison.Ordinal);
        Assert.Contains("ProfilesViewControl.ApplyAudioProfileControlValues(", source, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.ApplyAudioProfileEffectControlValues(", source, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.ApplyAudioProfileEffectControlText(", source, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerViewControl.ApplyAudioProfileMixerControlValues(", source, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerViewControl.ApplyAudioProfileMixerControlText(", source, StringComparison.Ordinal);
        Assert.Contains("ProfilesViewControl.BuildAudioProfileNameInput(", source, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.BuildAudioProfileEffectControlInputs(", source, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerViewControl.BuildAudioProfileMixerControlInputs(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("return HapticProfileValidator.Validate(_currentProfile with", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineGainValueText.Text = $\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadTextureSpeedFrequencyInfluenceValueText.Text = $\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SlipWheelLockSensitivityValueText.Text = $\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineGainSlider.Value = effects.EngineGain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MasterGainSlider.Value = values.MasterGain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileNameTextBox.Text = values.ProfileName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileName: ProfileNameTextBox.Text", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineEnabled: EngineEnabledCheckBox.IsChecked == true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MasterGainValue: MasterGainSlider.Value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private Slider EngineGainSlider =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private Slider MasterGainSlider =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private TextBox ProfileNameTextBox =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private TextBlock ProfileStatusText =>", source, StringComparison.Ordinal);
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
