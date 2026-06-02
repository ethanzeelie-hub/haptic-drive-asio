using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticProfileTests
{
    [Fact]
    public void DefaultProfile_UsesConservativeHardwareSafeValues()
    {
        var profile = HapticDriveProfile.Default;

        Assert.Equal(HapticDriveProfile.CurrentVersion, profile.Version);
        Assert.True(profile.Effects.Engine.IsEnabled);
        Assert.InRange(profile.Effects.Engine.Gain, 0f, 0.1f);
        Assert.InRange(profile.Effects.Impact.Gain, 0f, 0.25f);
        Assert.InRange(profile.Safety.OutputGain, 0f, AudioSafetyProcessorOptions.DefaultOutputGain);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, profile.Safety.OutputGainCeiling);
        Assert.False(profile.Mixer.IsMuted);
    }

    [Fact]
    public async Task ProfileSaveLoad_RoundTripsExpectedSettings()
    {
        var path = CreateTempProfilePath();
        var store = new HapticProfileStore();
        var profile = HapticDriveProfile.Default with
        {
            Name = "Round Trip",
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with
                {
                    IsEnabled = false,
                    Gain = 0.12f,
                    MinimumFrequencyHz = 30f,
                    MaximumFrequencyHz = 60f
                },
                GearShift = HapticDriveProfile.Default.Effects.GearShift with
                {
                    Gain = 0.22f,
                    PulseDurationMilliseconds = 120
                }
            },
            Mixer = HapticDriveProfile.Default.Mixer with
            {
                MasterGain = 0.45f,
                IsMuted = true
            }
        };

        var saveResult = await store.SaveAsync(profile, path);
        var loadResult = await store.LoadAsync(path);

        Assert.True(saveResult.Succeeded, saveResult.Message);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        Assert.NotNull(loadResult.Profile);
        Assert.Equal("Round Trip", loadResult.Profile.Name);
        Assert.False(loadResult.Profile.Effects.Engine.IsEnabled);
        Assert.Equal(0.12f, loadResult.Profile.Effects.Engine.Gain, precision: 6);
        Assert.Equal(120, loadResult.Profile.Effects.GearShift.PulseDurationMilliseconds);
        Assert.Equal(0.45f, loadResult.Profile.Mixer.MasterGain, precision: 6);
        Assert.True(loadResult.Profile.Mixer.IsMuted);
    }

    [Fact]
    public async Task ProfileLoad_MissingFileFailsSafely()
    {
        var store = new HapticProfileStore();

        var result = await store.LoadAsync(CreateTempProfilePath());

        Assert.False(result.Succeeded);
        Assert.Equal(HapticProfileLoadStatus.FileNotFound, result.Status);
    }

    [Fact]
    public async Task ProfileLoad_CorruptJsonFailsSafely()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(path, "{ this is not json");
        var store = new HapticProfileStore();

        var result = await store.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(HapticProfileLoadStatus.Corrupt, result.Status);
    }

    [Fact]
    public async Task ProfileLoad_UnsupportedVersionFailsSafely()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(path, """{"Version":99,"Name":"Future"}""");
        var store = new HapticProfileStore();

        var result = await store.LoadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Equal(HapticProfileLoadStatus.UnsupportedVersion, result.Status);
    }

    [Fact]
    public void ProfileValidation_RepairsInvalidValuesToSafeBounds()
    {
        var profile = HapticDriveProfile.Default with
        {
            Name = "   ",
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with
                {
                    Gain = 4f,
                    MinimumFrequencyHz = -10f,
                    MaximumFrequencyHz = 2f
                },
                RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with
                {
                    Gain = 2f
                },
                Slip = HapticDriveProfile.Default.Effects.Slip with
                {
                    SlipRatioThreshold = float.NaN
                }
            },
            Mixer = HapticDriveProfile.Default.Mixer with
            {
                MasterGain = float.PositiveInfinity
            },
            Safety = HapticDriveProfile.Default.Safety with
            {
                OutputGain = 2f,
                OutputGainCeiling = 10f
            }
        };

        var result = HapticProfileValidator.Validate(profile);

        Assert.True(result.IsSupportedVersion);
        Assert.True(result.WasRepaired);
        Assert.Equal("Default Conservative", result.Profile.Name);
        Assert.InRange(result.Profile.Effects.Engine.Gain, 0f, 0.4f);
        Assert.InRange(result.Profile.Effects.RoadTexture.Gain, 0f, 0.25f);
        Assert.Equal(HapticDriveProfile.Default.Effects.Slip.SlipRatioThreshold, result.Profile.Effects.Slip.SlipRatioThreshold);
        Assert.Equal(HapticDriveProfile.Default.Mixer.MasterGain, result.Profile.Mixer.MasterGain);
        Assert.Equal(0.5f, result.Profile.Safety.OutputGain, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, result.Profile.Safety.OutputGainCeiling);
    }

    [Fact]
    public void ProfileMapsEffectEnabledAndGainIntoEffectOptions()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                Engine = HapticDriveProfile.Default.Effects.Engine with
                {
                    IsEnabled = false,
                    Gain = 0.2f
                },
                Kerb = HapticDriveProfile.Default.Effects.Kerb with
                {
                    Gain = 0.3f
                }
            }
        };

        var options = profile.ToEffectOptions();

        Assert.False(options.Engine.IsEnabled);
        Assert.Equal(0.2f, options.Engine.Gain, precision: 6);
        Assert.Equal(0.3f, options.Kerb.Gain, precision: 6);
    }

    [Fact]
    public void ProfileMapsMixerAndSafetyWithoutPersistingEmergencyMute()
    {
        var profile = HapticDriveProfile.Default with
        {
            Mixer = new HapticMixerTuning(MasterGain: 0.4f, IsMuted: true),
            Safety = new HapticSafetyTuning(OutputGain: 0.3f, OutputGainCeiling: 0.5f, LimiterEnabled: false)
        };

        var mixer = profile.ToMixerSettings(emergencyMute: true);
        var safety = profile.ToSafetyOptions(emergencyMute: true);
        var clearedMixer = profile.ToMixerSettings(emergencyMute: false);

        Assert.Equal(0.4f, mixer.MasterGain, precision: 6);
        Assert.True(mixer.IsMuted);
        Assert.True(mixer.EmergencyMute);
        Assert.False(clearedMixer.EmergencyMute);
        Assert.Equal(0.3f, safety.OutputGain, precision: 6);
        Assert.Equal(0.5f, safety.OutputGainCeiling, precision: 6);
        Assert.False(safety.LimiterEnabled);
        Assert.True(safety.EmergencyMute);
    }

    [Fact]
    public void EffectEngine_UpdateOptionsAppliesTuningDeterministically()
    {
        var engine = new HapticEffectEngine(new AudioSampleFormat(1_000, 1, 10));
        var tunedOptions = HapticEffectEngineOptions.Default with
        {
            Engine = HapticEffectEngineOptions.Default.Engine with
            {
                IsEnabled = false,
                Gain = 0.3f
            }
        };

        engine.UpdateOptions(tunedOptions);

        Assert.False(engine.Options.Engine.IsEnabled);
        Assert.Equal(0.3f, engine.Options.Engine.Gain, precision: 6);
        Assert.False(engine.GetSnapshot().Engine.IsEnabled);
    }

    [Fact]
    public async Task AudioDiagnosticsSnapshot_CanBeCreatedWithoutHardwareOrTelemetry()
    {
        var configuration = new AudioOutputConfiguration(1_000, 1, 10);
        var format = AudioSampleFormat.FromConfiguration(configuration);
        await using var outputDevice = new NullAudioOutputDevice();
        await using var testBench = new AudioTestBench(configuration, outputDevice);
        var engine = new HapticEffectEngine(format);
        var pipeline = new AudioRenderPipeline(format);
        var outputBuffer = AudioSampleBuffer.Allocate(format);
        var outputStatus = (await outputDevice.OpenAsync(configuration)).Status;
        var pipelineSnapshot = pipeline.Process([], outputBuffer);

        var snapshot = AudioRuntimeDiagnosticsSnapshot.Create(
            outputStatus,
            engine.GetSnapshot(),
            pipelineSnapshot,
            testBench.GetSnapshot());

        Assert.True(snapshot.HardwareAbsentMode);
        Assert.False(snapshot.RequiresPhysicalHardware);
        Assert.Equal(AudioOutputDeviceKind.Null, snapshot.Output.Kind);
        Assert.Equal(0, snapshot.ActiveEffectCount);
        Assert.Equal(0f, snapshot.OutputPeakLevel);
    }

    private static string CreateTempProfilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Audio.Tests", "Profiles");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdprofile.json");
    }
}
