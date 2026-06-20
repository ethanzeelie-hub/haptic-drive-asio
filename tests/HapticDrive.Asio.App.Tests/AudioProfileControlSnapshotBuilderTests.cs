using System.Text.Json;
using HapticDrive.Asio.App;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;

namespace HapticDrive.Asio.App.Tests;

public sealed class AudioProfileControlSnapshotBuilderTests
{
    [Fact]
    public void BuildProfile_MapsRepresentativeControlsAndPreservesHiddenProfileFields()
    {
        var currentProfile = HapticDriveProfile.Default with
        {
            Name = "Current",
            Effects = HapticDriveProfile.Default.Effects with
            {
                GearShift = HapticDriveProfile.Default.Effects.GearShift with { PulseFrequencyHz = 77f },
                Kerb = HapticDriveProfile.Default.Effects.Kerb with
                {
                    MinimumSpeedKph = 9f,
                    FullIntensitySpeedKph = 210f
                },
                Impact = HapticDriveProfile.Default.Effects.Impact with
                {
                    PulseFrequencyHz = 63f,
                    CooldownMilliseconds = 333,
                    VerticalGDeltaThreshold = 1.8f
                },
                Slip = HapticDriveProfile.Default.Effects.Slip with
                {
                    MinimumSpeedKph = 14f,
                    SlipAngleThresholdRadians = 0.07f
                }
            }
        };

        var profile = AudioProfileControlSnapshotBuilder.BuildProfile(
            currentProfile,
            new AudioProfileControlInputs(
                ProfileName: "  Stage 21E  ",
                EngineEnabled: true,
                EngineGainValue: 0.61d,
                EngineMinimumFrequencyValue: 33d,
                EngineMaximumFrequencyValue: 66d,
                GearShiftEnabled: true,
                GearShiftGainValue: 0.45d,
                GearShiftDurationValue: 121.2d,
                KerbEnabled: true,
                KerbGainValue: 0.34d,
                KerbBaseFrequencyValue: 47d,
                ImpactEnabled: true,
                ImpactGainValue: 0.29d,
                ImpactDurationValue: 156.7d,
                SharedRoadSignalEnabled: true,
                Bst1RoadOutputEnabled: false,
                RoadTextureGainValue: 0.82d,
                RoadTextureMinimumSpeedValue: 17d,
                RoadTextureSpeedReferenceValue: 250d,
                RoadTextureLowSpeedFrequencyValue: 42d,
                RoadTextureHighSpeedFrequencyValue: 71d,
                RoadTextureSpeedFrequencyInfluenceValue: 0.41d,
                RoadTextureGrainAmountValue: 0.23d,
                SlipWheelSlipEnabled: true,
                SlipWheelSlipGainValue: 0.36d,
                SlipWheelSlipFrequencyValue: 58d,
                SlipWheelSlipNoiseValue: 0.27d,
                SlipWheelLockEnabled: true,
                SlipWheelLockGainValue: 0.49d,
                SlipWheelLockFrequencyValue: 73d,
                SlipWheelLockNoiseValue: 0.31d,
                SlipWheelLockSensitivityValue: 0.22d,
                SlipThresholdValue: 0.18d,
                MasterGainValue: 0.57d,
                MixerMuted: true,
                SafetyOutputGainValue: 0.74d));

        Assert.Equal("Stage 21E", profile.Name);
        Assert.True(profile.Effects.Engine.IsEnabled);
        Assert.Equal(0.61f, profile.Effects.Engine.Gain, precision: 6);
        Assert.Equal(33f, profile.Effects.Engine.MinimumFrequencyHz, precision: 6);
        Assert.Equal(66f, profile.Effects.Engine.MaximumFrequencyHz, precision: 6);
        Assert.True(profile.Effects.GearShift.IsEnabled);
        Assert.Equal(0.45f, profile.Effects.GearShift.Gain, precision: 6);
        Assert.Equal(121, profile.Effects.GearShift.PulseDurationMilliseconds);
        Assert.Equal(77f, profile.Effects.GearShift.PulseFrequencyHz, precision: 6);
        Assert.True(profile.Effects.Kerb.IsEnabled);
        Assert.Equal(0.34f, profile.Effects.Kerb.Gain, precision: 6);
        Assert.Equal(47f, profile.Effects.Kerb.BaseFrequencyHz, precision: 6);
        Assert.Equal(9f, profile.Effects.Kerb.MinimumSpeedKph, precision: 6);
        Assert.Equal(210f, profile.Effects.Kerb.FullIntensitySpeedKph, precision: 6);
        Assert.True(profile.Effects.Impact.IsEnabled);
        Assert.Equal(0.29f, profile.Effects.Impact.Gain, precision: 6);
        Assert.Equal(157, profile.Effects.Impact.PulseDurationMilliseconds);
        Assert.Equal(63f, profile.Effects.Impact.PulseFrequencyHz, precision: 6);
        Assert.Equal(333, profile.Effects.Impact.CooldownMilliseconds);
        Assert.Equal(1.8f, profile.Effects.Impact.VerticalGDeltaThreshold, precision: 6);
        Assert.True(profile.Effects.RoadTexture.IsEnabled);
        Assert.False(profile.Effects.RoadTexture.Bst1OutputEnabled ?? true);
        Assert.Equal(0.82f, profile.Effects.RoadTexture.Gain, precision: 6);
        Assert.Equal(17f, profile.Effects.RoadTexture.MinimumSpeedKph, precision: 6);
        Assert.Equal(250f, profile.Effects.RoadTexture.FullIntensitySpeedKph, precision: 6);
        Assert.Equal(42f, profile.Effects.RoadTexture.LowSpeedFrequencyHz, precision: 6);
        Assert.Equal(71f, profile.Effects.RoadTexture.HighSpeedFrequencyHz, precision: 6);
        Assert.Equal(0.41f, profile.Effects.RoadTexture.SpeedFrequencyInfluence, precision: 6);
        Assert.Equal(0.23f, profile.Effects.RoadTexture.GrainAmount, precision: 6);
        Assert.True(profile.Effects.Slip.IsEnabled);
        Assert.True(profile.Effects.Slip.WheelSlipEnabled ?? false);
        Assert.True(profile.Effects.Slip.WheelLockEnabled ?? false);
        Assert.Equal(0.36f, profile.Effects.Slip.Gain, precision: 6);
        Assert.Equal(58f, profile.Effects.Slip.BaseFrequencyHz, precision: 6);
        Assert.Equal(14f, profile.Effects.Slip.MinimumSpeedKph, precision: 6);
        Assert.Equal(0.18f, profile.Effects.Slip.SlipRatioThreshold, precision: 6);
        Assert.Equal(0.07f, profile.Effects.Slip.SlipAngleThresholdRadians, precision: 6);
        Assert.Equal(0.27f, profile.Effects.Slip.WheelSlipNoiseAmount ?? -1f, precision: 6);
        Assert.Equal(0.49f, profile.Effects.Slip.WheelLockGain ?? -1f, precision: 6);
        Assert.Equal(73f, profile.Effects.Slip.WheelLockFrequencyHz ?? -1f, precision: 6);
        Assert.Equal(0.31f, profile.Effects.Slip.WheelLockNoiseAmount ?? -1f, precision: 6);
        Assert.Equal(0.22f, profile.Effects.Slip.WheelLockWheelSpeedRatioThreshold ?? -1f, precision: 6);
        Assert.Equal(0.57f, profile.Mixer.MasterGain, precision: 6);
        Assert.True(profile.Mixer.IsMuted);
        Assert.Equal(0.74f, profile.Safety.OutputGain, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, profile.Safety.OutputGainCeiling, precision: 6);
        Assert.True(profile.Safety.LimiterEnabled);
    }

    [Fact]
    public void BuildProfile_RepairsInvalidValuesAndFallsBackToCurrentName()
    {
        var currentProfile = HapticDriveProfile.Default with { Name = "Existing Profile" };

        var profile = AudioProfileControlSnapshotBuilder.BuildProfile(
            currentProfile,
            new AudioProfileControlInputs(
                ProfileName: "   ",
                EngineEnabled: true,
                EngineGainValue: 2d,
                EngineMinimumFrequencyValue: 70d,
                EngineMaximumFrequencyValue: 35d,
                GearShiftEnabled: true,
                GearShiftGainValue: -1d,
                GearShiftDurationValue: 999.4d,
                KerbEnabled: true,
                KerbGainValue: 4d,
                KerbBaseFrequencyValue: 150d,
                ImpactEnabled: true,
                ImpactGainValue: 1.5d,
                ImpactDurationValue: 1d,
                SharedRoadSignalEnabled: true,
                Bst1RoadOutputEnabled: true,
                RoadTextureGainValue: 2d,
                RoadTextureMinimumSpeedValue: 90d,
                RoadTextureSpeedReferenceValue: 10d,
                RoadTextureLowSpeedFrequencyValue: 75d,
                RoadTextureHighSpeedFrequencyValue: 10d,
                RoadTextureSpeedFrequencyInfluenceValue: 2d,
                RoadTextureGrainAmountValue: double.PositiveInfinity,
                SlipWheelSlipEnabled: true,
                SlipWheelSlipGainValue: -1d,
                SlipWheelSlipFrequencyValue: 200d,
                SlipWheelSlipNoiseValue: double.NaN,
                SlipWheelLockEnabled: true,
                SlipWheelLockGainValue: 3d,
                SlipWheelLockFrequencyValue: 2d,
                SlipWheelLockNoiseValue: -2d,
                SlipWheelLockSensitivityValue: 0d,
                SlipThresholdValue: 0d,
                MasterGainValue: double.NaN,
                MixerMuted: false,
                SafetyOutputGainValue: 2d));

        Assert.Equal("Existing Profile", profile.Name);
        Assert.Equal(1f, profile.Effects.Engine.Gain, precision: 6);
        Assert.Equal(70f, profile.Effects.Engine.MinimumFrequencyHz, precision: 6);
        Assert.Equal(70f, profile.Effects.Engine.MaximumFrequencyHz, precision: 6);
        Assert.Equal(0f, profile.Effects.GearShift.Gain, precision: 6);
        Assert.Equal(250, profile.Effects.GearShift.PulseDurationMilliseconds);
        Assert.Equal(1f, profile.Effects.Kerb.Gain, precision: 6);
        Assert.Equal(120f, profile.Effects.Kerb.BaseFrequencyHz, precision: 6);
        Assert.Equal(1f, profile.Effects.Impact.Gain, precision: 6);
        Assert.Equal(10, profile.Effects.Impact.PulseDurationMilliseconds);
        Assert.Equal(1f, profile.Effects.RoadTexture.Gain, precision: 6);
        Assert.Equal(80f, profile.Effects.RoadTexture.MinimumSpeedKph, precision: 6);
        Assert.Equal(HapticDriveProfile.Default.Effects.RoadTexture.FullIntensitySpeedKph, profile.Effects.RoadTexture.FullIntensitySpeedKph, precision: 6);
        Assert.Equal(70f, profile.Effects.RoadTexture.LowSpeedFrequencyHz, precision: 6);
        Assert.Equal(70f, profile.Effects.RoadTexture.HighSpeedFrequencyHz, precision: 6);
        Assert.Equal(1f, profile.Effects.RoadTexture.SpeedFrequencyInfluence, precision: 6);
        Assert.Equal(HapticDriveProfile.Default.Effects.RoadTexture.GrainAmount, profile.Effects.RoadTexture.GrainAmount, precision: 6);
        Assert.Equal(0f, profile.Effects.Slip.Gain, precision: 6);
        Assert.Equal(120f, profile.Effects.Slip.BaseFrequencyHz, precision: 6);
        Assert.Equal(HapticDriveProfile.Default.Effects.Slip.WheelSlipNoiseAmount, profile.Effects.Slip.WheelSlipNoiseAmount);
        Assert.Equal(1f, profile.Effects.Slip.WheelLockGain ?? -1f, precision: 6);
        Assert.Equal(5f, profile.Effects.Slip.WheelLockFrequencyHz ?? -1f, precision: 6);
        Assert.Equal(0f, profile.Effects.Slip.WheelLockNoiseAmount ?? -1f, precision: 6);
        Assert.Equal(0.05f, profile.Effects.Slip.WheelLockWheelSpeedRatioThreshold ?? -1f, precision: 6);
        Assert.Equal(0.01f, profile.Effects.Slip.SlipRatioThreshold, precision: 6);
        Assert.Equal(HapticDriveProfile.Default.Mixer.MasterGain, profile.Mixer.MasterGain, precision: 6);
        Assert.Equal(1f, profile.Safety.OutputGain, precision: 6);
        Assert.Equal(AudioSafetyProcessorOptions.DefaultOutputGainCeiling, profile.Safety.OutputGainCeiling, precision: 6);
        Assert.True(profile.Safety.LimiterEnabled);
    }

    [Fact]
    public void BuildApplicationPlan_MapsLegacyFallbackValuesAndDisplayText()
    {
        var profile = HapticDriveProfile.Default with
        {
            Name = "Legacy",
            Effects = HapticDriveProfile.Default.Effects with
            {
                RoadTexture = new RoadTextureTuning(
                    IsEnabled: true,
                    Gain: 0.25f,
                    MinimumSpeedKph: 5f,
                    FullIntensitySpeedKph: 160f),
                Slip = new SlipTuning(
                    IsEnabled: true,
                    Gain: 0.37f,
                    BaseFrequencyHz: 54f,
                    MinimumSpeedKph: 8f,
                    SlipRatioThreshold: 0.12f,
                    SlipAngleThresholdRadians: 0.08f)
            },
            Mixer = HapticDriveProfile.Default.Mixer with { MasterGain = 0.45f },
            Safety = HapticDriveProfile.Default.Safety with { OutputGain = 0.65f }
        };

        var plan = AudioProfileControlSnapshotBuilder.BuildApplicationPlan(profile);
        var effects = plan.ControlValues.Effects;
        var effectText = plan.TextValues.Effects;

        Assert.Equal("Legacy", plan.SafeProfile.Name);
        Assert.True(effects.SharedRoadSignalEnabled);
        Assert.True(effects.Bst1RoadOutputEnabled);
        Assert.True(effects.SlipWheelSlipEnabled);
        Assert.True(effects.SlipWheelLockEnabled);
        Assert.Equal(0.37f, effects.SlipWheelSlipGain, precision: 6);
        Assert.Equal(54f, effects.SlipWheelSlipFrequencyHz, precision: 6);
        Assert.Equal(0.37f, effects.SlipWheelLockGain, precision: 6);
        Assert.Equal(0.12f, effects.SlipThreshold, precision: 6);
        Assert.Equal("37%", effectText.SlipWheelSlipGainText);
        Assert.Equal("54 Hz", effectText.SlipWheelSlipFrequencyText);
        Assert.Equal($"{SlipEffectOptions.Default.WheelLockFrequencyHz:0} Hz", effectText.SlipWheelLockFrequencyText);
        Assert.Equal($"{SlipEffectOptions.Default.WheelLockNoiseAmount:P0}", effectText.SlipWheelLockNoiseText);
        Assert.Equal("45%", plan.TextValues.MasterGainText);
        Assert.Equal("65%", plan.TextValues.SafetyOutputGainText);
    }

    [Fact]
    public void BuildApplicationPlan_NullProfileFallsBackToDefaultAndSerializedProfileContainsNoRuntimeState()
    {
        var plan = AudioProfileControlSnapshotBuilder.BuildApplicationPlan(profile: null);
        var json = JsonSerializer.Serialize(plan.SafeProfile);

        Assert.Equal("Current Rig Defaults", plan.SafeProfile.Name);
        Assert.Equal(HapticDriveProfile.CurrentVersion, plan.SafeProfile.Version);
        Assert.DoesNotContain("DirectControl", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Armed", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Emergency", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Running", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Started", json, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandHistory", json, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteHistory", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationPath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("CapturePath", json, StringComparison.Ordinal);
    }
}
