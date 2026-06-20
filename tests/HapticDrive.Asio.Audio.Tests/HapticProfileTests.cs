using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Persistence;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticProfileTests
{
    [Fact]
    public void DefaultProfile_UsesStage18rCCurrentRigDefaults()
    {
        var profile = HapticDriveProfile.Default;

        Assert.Equal(HapticDriveProfile.CurrentVersion, profile.Version);
        Assert.False(profile.Effects.Engine.IsEnabled);
        Assert.False(profile.Effects.GearShift.IsEnabled);
        Assert.False(profile.Effects.Kerb.IsEnabled);
        Assert.False(profile.Effects.Impact.IsEnabled);
        Assert.True(profile.Effects.RoadTexture.IsEnabled);
        Assert.True(profile.Effects.RoadTexture.Bst1OutputEnabled);
        Assert.Equal(1f, profile.Effects.RoadTexture.Gain, precision: 6);
        Assert.Equal(330f, profile.Effects.RoadTexture.FullIntensitySpeedKph, precision: 6);
        Assert.Equal(40f, profile.Effects.RoadTexture.LowSpeedFrequencyHz, precision: 6);
        Assert.Equal(68f, profile.Effects.RoadTexture.HighSpeedFrequencyHz, precision: 6);
        Assert.Equal(0.75f, profile.Effects.RoadTexture.SpeedFrequencyInfluence, precision: 6);
        Assert.Equal(0.18f, profile.Effects.RoadTexture.GrainAmount, precision: 6);
        Assert.False(profile.Effects.Slip.IsEnabled);
        Assert.False(profile.Effects.Slip.WheelSlipEnabled ?? true);
        Assert.False(profile.Effects.Slip.WheelLockEnabled ?? true);
        Assert.Equal(0.5f, profile.Effects.Slip.Gain, precision: 6);
        Assert.Equal(0.5f, profile.Effects.Slip.WheelLockGain ?? -1f, precision: 6);
        Assert.Equal(0.5f, profile.Effects.Engine.Gain, precision: 6);
        Assert.Equal(0.5f, profile.Effects.GearShift.Gain, precision: 6);
        Assert.Equal(0.5f, profile.Effects.Impact.Gain, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGain, profile.Safety.OutputGain, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, profile.Safety.OutputGainCeiling);
        Assert.True(profile.Safety.LimiterEnabled);
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
                },
                RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with
                {
                    FullIntensitySpeedKph = 320f,
                    LowSpeedFrequencyHz = 38f,
                    HighSpeedFrequencyHz = 72f,
                    SpeedFrequencyInfluence = 0.55f,
                    GrainAmount = 0.24f
                },
                Slip = HapticDriveProfile.Default.Effects.Slip with
                {
                    IsEnabled = true,
                    Gain = 0.34f,
                    BaseFrequencyHz = 58f,
                    SlipRatioThreshold = 0.14f,
                    WheelSlipEnabled = true,
                    WheelSlipNoiseAmount = 0.28f,
                    WheelLockEnabled = true,
                    WheelLockGain = 0.46f,
                    WheelLockFrequencyHz = 72f,
                    WheelLockNoiseAmount = 0.35f,
                    WheelLockWheelSpeedRatioThreshold = 0.22f
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
        Assert.Equal(320f, loadResult.Profile.Effects.RoadTexture.FullIntensitySpeedKph, precision: 6);
        Assert.Equal(38f, loadResult.Profile.Effects.RoadTexture.LowSpeedFrequencyHz, precision: 6);
        Assert.Equal(72f, loadResult.Profile.Effects.RoadTexture.HighSpeedFrequencyHz, precision: 6);
        Assert.Equal(0.55f, loadResult.Profile.Effects.RoadTexture.SpeedFrequencyInfluence, precision: 6);
        Assert.Equal(0.24f, loadResult.Profile.Effects.RoadTexture.GrainAmount, precision: 6);
        Assert.True(loadResult.Profile.Effects.Slip.WheelSlipEnabled ?? false);
        Assert.True(loadResult.Profile.Effects.Slip.WheelLockEnabled ?? false);
        Assert.Equal(0.34f, loadResult.Profile.Effects.Slip.Gain, precision: 6);
        Assert.Equal(58f, loadResult.Profile.Effects.Slip.BaseFrequencyHz, precision: 6);
        Assert.Equal(0.28f, loadResult.Profile.Effects.Slip.WheelSlipNoiseAmount ?? -1f, precision: 6);
        Assert.Equal(0.46f, loadResult.Profile.Effects.Slip.WheelLockGain ?? -1f, precision: 6);
        Assert.Equal(72f, loadResult.Profile.Effects.Slip.WheelLockFrequencyHz ?? -1f, precision: 6);
        Assert.Equal(0.35f, loadResult.Profile.Effects.Slip.WheelLockNoiseAmount ?? -1f, precision: 6);
        Assert.Equal(0.22f, loadResult.Profile.Effects.Slip.WheelLockWheelSpeedRatioThreshold ?? -1f, precision: 6);
        Assert.Equal(0.45f, loadResult.Profile.Mixer.MasterGain, precision: 6);
        Assert.True(loadResult.Profile.Mixer.IsMuted);
    }

    [Fact]
    public async Task ProfileSave_RefreshesLastKnownGoodBackup()
    {
        var path = CreateTempProfilePath();
        var backupPath = DocumentBackupFile.GetBackupPath(path);
        var store = new HapticProfileStore();

        var saveResult = await store.SaveAsync(HapticDriveProfile.Default, path);

        Assert.True(saveResult.Succeeded, saveResult.Message);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(await File.ReadAllTextAsync(path), await File.ReadAllTextAsync(backupPath));
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
    public async Task ProfileLoad_CorruptPrimary_RecoversFromBackupSnapshot()
    {
        var path = CreateTempProfilePath();
        var store = new HapticProfileStore();
        Assert.True((await store.SaveAsync(HapticDriveProfile.Default with { Name = "Backup copy" }, path)).Succeeded);
        await File.WriteAllTextAsync(path, "{ broken");

        var result = await store.LoadAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal("Backup copy", result.Profile.Name);
        Assert.Equal("Profile recovered from backup snapshot.", result.Message);
    }

    [Fact]
    public async Task ProfileLoad_CorruptPrimaryAndBackup_RecoversFromBackupHistorySnapshot()
    {
        var path = CreateTempProfilePath();
        var backupPath = DocumentBackupFile.GetBackupPath(path);
        var store = new HapticProfileStore();

        Assert.True((await store.SaveAsync(HapticDriveProfile.Default with { Name = "Older copy" }, path)).Succeeded);
        await Task.Delay(20);
        Assert.True((await store.SaveAsync(HapticDriveProfile.Default with { Name = "History copy" }, path)).Succeeded);

        await File.WriteAllTextAsync(path, "{ broken");
        await File.WriteAllTextAsync(backupPath, "{ broken");

        var result = await store.LoadAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal("History copy", result.Profile.Name);
        Assert.Equal("Profile recovered from backup history snapshot.", result.Message);
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
    public async Task ProfileLoad_VersionlessLegacyProfileMigratesToCurrentVersion()
    {
        var path = CreateTempProfilePath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "Name": "Legacy",
              "Effects": {
                "Engine": {
                  "IsEnabled": false,
                  "Gain": 0.2,
                  "MinimumFrequencyHz": 30,
                  "MaximumFrequencyHz": 60
                }
              }
            }
            """);
        var store = new HapticProfileStore();

        var result = await store.LoadAsync(path);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal(HapticDriveProfile.CurrentVersion, result.Profile.Version);
        Assert.True(result.WasRepaired);
        Assert.Contains(result.ValidationMessages, message => message.Contains("migrated to version 1", StringComparison.Ordinal));
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
                    SlipRatioThreshold = float.NaN,
                    WheelSlipNoiseAmount = float.PositiveInfinity,
                    WheelLockGain = -2f,
                    WheelLockFrequencyHz = 999f,
                    WheelLockNoiseAmount = float.NaN,
                    WheelLockWheelSpeedRatioThreshold = 0f
                }
            },
            Mixer = HapticDriveProfile.Default.Mixer with
            {
                MasterGain = float.PositiveInfinity
            },
            Safety = HapticDriveProfile.Default.Safety with
            {
                OutputGain = 2f,
                OutputGainCeiling = 10f,
                LimiterEnabled = false
            }
        };

        var result = HapticProfileValidator.Validate(profile);

        Assert.True(result.IsSupportedVersion);
        Assert.True(result.WasRepaired);
        Assert.Equal("Current Rig Defaults", result.Profile.Name);
        Assert.InRange(result.Profile.Effects.Engine.Gain, 0f, 1f);
        Assert.InRange(result.Profile.Effects.RoadTexture.Gain, 0f, 1f);
        Assert.Equal(HapticDriveProfile.Default.Effects.Slip.SlipRatioThreshold, result.Profile.Effects.Slip.SlipRatioThreshold);
        Assert.Equal(HapticDriveProfile.Default.Effects.Slip.WheelSlipNoiseAmount, result.Profile.Effects.Slip.WheelSlipNoiseAmount);
        Assert.Equal(0f, result.Profile.Effects.Slip.WheelLockGain ?? -1f, precision: 6);
        Assert.Equal(120f, result.Profile.Effects.Slip.WheelLockFrequencyHz ?? -1f, precision: 6);
        Assert.Equal(HapticDriveProfile.Default.Effects.Slip.WheelLockNoiseAmount, result.Profile.Effects.Slip.WheelLockNoiseAmount);
        Assert.Equal(0.05f, result.Profile.Effects.Slip.WheelLockWheelSpeedRatioThreshold ?? -1f, precision: 6);
        Assert.Equal(HapticDriveProfile.Default.Mixer.MasterGain, result.Profile.Mixer.MasterGain);
        Assert.Equal(1f, result.Profile.Safety.OutputGain, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, result.Profile.Safety.OutputGainCeiling);
        Assert.True(result.Profile.Safety.LimiterEnabled);
    }

    [Fact]
    public void ProfileValidation_AllowsFullBst1AndNonRoadGainRange()
    {
        var defaultProfile = HapticDriveProfile.Default;
        var tunedProfile = HapticProfileValidator.Validate(defaultProfile with
        {
            Effects = defaultProfile.Effects with
            {
                Engine = defaultProfile.Effects.Engine with { Gain = 1f },
                RoadTexture = defaultProfile.Effects.RoadTexture with { Gain = 1f },
                Slip = defaultProfile.Effects.Slip with { Gain = 1f }
            }
        }).Profile;

        Assert.Equal(1f, defaultProfile.Effects.RoadTexture.Gain, precision: 6);
        Assert.Equal(1f, tunedProfile.Effects.Engine.Gain, precision: 6);
        Assert.Equal(1f, tunedProfile.Effects.RoadTexture.Gain, precision: 6);
        Assert.Equal(1f, tunedProfile.Effects.Slip.Gain, precision: 6);
        Assert.Equal(1f, tunedProfile.ToEffectOptions().RoadTexture.Gain, precision: 6);
    }

    [Fact]
    public void SlipProfile_MigratesLegacyCombinedSettingsIntoSlipAndLockOutputs()
    {
        var legacySlip = new SlipTuning(
            IsEnabled: true,
            Gain: 0.37f,
            BaseFrequencyHz: 54f,
            MinimumSpeedKph: 8f,
            SlipRatioThreshold: 0.12f,
            SlipAngleThresholdRadians: 0.08f);
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with { Slip = legacySlip }
        };

        var validated = HapticProfileValidator.Validate(profile).Profile;
        var options = validated.ToEffectOptions().Slip;

        Assert.True(validated.Effects.Slip.WheelSlipEnabled ?? false);
        Assert.True(validated.Effects.Slip.WheelLockEnabled ?? false);
        Assert.True(options.IsEnabled);
        Assert.True(options.WheelSlipEnabled);
        Assert.True(options.WheelLockEnabled);
        Assert.Equal(0.37f, options.WheelSlipGain, precision: 6);
        Assert.Equal(0.37f, options.WheelLockGain, precision: 6);
        Assert.Equal(54f, options.WheelSlipFrequencyHz, precision: 6);
        Assert.Equal(SlipEffectOptions.Default.WheelLockFrequencyHz, options.WheelLockFrequencyHz, precision: 6);
    }

    [Fact]
    public void RoadTextureProfile_MissingBst1OutputEnableInheritsOldSharedRoadSetting()
    {
        var oldStyleRoad = new RoadTextureTuning(
            IsEnabled: true,
            Gain: 0.25f,
            MinimumSpeedKph: 5f,
            FullIntensitySpeedKph: 160f);
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with { RoadTexture = oldStyleRoad }
        };

        var validated = HapticProfileValidator.Validate(profile).Profile;

        Assert.True(validated.Effects.RoadTexture.IsEnabled);
        Assert.True(validated.Effects.RoadTexture.Bst1OutputEnabled);
        Assert.Equal(HapticDriveProfile.Default.Effects.RoadTexture.LowSpeedFrequencyHz, validated.Effects.RoadTexture.LowSpeedFrequencyHz);
        Assert.Equal(HapticDriveProfile.Default.Effects.RoadTexture.HighSpeedFrequencyHz, validated.Effects.RoadTexture.HighSpeedFrequencyHz);
        Assert.Equal(HapticDriveProfile.Default.Effects.RoadTexture.SpeedFrequencyInfluence, validated.Effects.RoadTexture.SpeedFrequencyInfluence);
        Assert.Equal(HapticDriveProfile.Default.Effects.RoadTexture.GrainAmount, validated.Effects.RoadTexture.GrainAmount);
        Assert.True(validated.ToEffectOptions().RoadTexture.IsEnabled);
        Assert.True(validated.ToEffectOptions().RoadTexture.Bst1OutputEnabled);
    }

    [Fact]
    public void RoadTextureProfile_MapsSharedSignalAndBst1OutputIndependently()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with
                {
                    IsEnabled = true,
                    Bst1OutputEnabled = false,
                    Gain = 0.75f
                }
            }
        };

        var options = profile.ToEffectOptions().RoadTexture;

        Assert.True(options.IsEnabled);
        Assert.False(options.Bst1OutputEnabled);
        Assert.Equal(0.75f, options.Gain, precision: 6);
    }

    [Fact]
    public void RoadTextureProfile_MapsNewFrequencySpeedAndGrainFields()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with
                {
                    FullIntensitySpeedKph = 330f,
                    LowSpeedFrequencyHz = 36f,
                    HighSpeedFrequencyHz = 74f,
                    SpeedFrequencyInfluence = 0.65f,
                    GrainAmount = 0.30f
                }
            }
        };

        var options = profile.ToEffectOptions().RoadTexture;

        Assert.Equal(330f, options.FullIntensitySpeedKph, precision: 6);
        Assert.Equal(36f, options.Bst1LowSpeedFrequencyHz, precision: 6);
        Assert.Equal(74f, options.Bst1HighSpeedFrequencyHz, precision: 6);
        Assert.Equal(0.65f, options.Bst1SpeedFrequencyInfluence, precision: 6);
        Assert.Equal(0.30f, options.Bst1GrainAmount, precision: 6);
    }

    [Fact]
    public void RoadTextureProfile_ClampsNewFrequencySpeedAndGrainFieldsSafely()
    {
        var profile = HapticDriveProfile.Default with
        {
            Effects = HapticDriveProfile.Default.Effects with
            {
                RoadTexture = HapticDriveProfile.Default.Effects.RoadTexture with
                {
                    FullIntensitySpeedKph = 999f,
                    LowSpeedFrequencyHz = 99f,
                    HighSpeedFrequencyHz = 10f,
                    SpeedFrequencyInfluence = 5f,
                    GrainAmount = -1f
                }
            }
        };

        var validated = HapticProfileValidator.Validate(profile).Profile.Effects.RoadTexture;

        Assert.Equal(360f, validated.FullIntensitySpeedKph, precision: 6);
        Assert.Equal(70f, validated.LowSpeedFrequencyHz, precision: 6);
        Assert.Equal(70f, validated.HighSpeedFrequencyHz, precision: 6);
        Assert.Equal(1f, validated.SpeedFrequencyInfluence, precision: 6);
        Assert.Equal(0f, validated.GrainAmount, precision: 6);
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
    public void ProfileMapsMixerAndInternalSafetyWithoutPersistingEmergencyMute()
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
        Assert.Equal(1f, safety.OutputGainCeiling, precision: 6);
        Assert.True(safety.LimiterEnabled);
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
    public void EffectEngineSnapshot_ExposesGenericActivityItemsForPresenterSummaries()
    {
        var engine = new HapticEffectEngine(new AudioSampleFormat(1_000, 1, 10));

        var snapshot = engine.GetSnapshot();

        Assert.Collection(
            snapshot.ActivityItems,
            item =>
            {
                Assert.Equal("engine", item.Label);
                Assert.Equal("idle", item.StatusText);
            },
            item =>
            {
                Assert.Equal("gear", item.Label);
                Assert.Equal("idle", item.StatusText);
            },
            item =>
            {
                Assert.Equal("kerb", item.Label);
                Assert.Equal("idle", item.StatusText);
            },
            item =>
            {
                Assert.Equal("impact", item.Label);
                Assert.Equal("idle", item.StatusText);
            },
            item =>
            {
                Assert.Equal("road", item.Label);
                Assert.Equal("idle", item.StatusText);
            },
            item =>
            {
                Assert.Equal("slip", item.Label);
                Assert.Equal("idle", item.StatusText);
            });
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
