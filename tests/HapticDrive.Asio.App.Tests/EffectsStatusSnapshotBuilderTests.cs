using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.App.Tests;

public sealed class EffectsStatusSnapshotBuilderTests
{
    [Fact]
    public void Build_MapsRuntimeAndOptionStateIntoStructuredSnapshot()
    {
        var runtime = new HapticEffectEngineSnapshot(
            Engine: new EngineVibrationEffectSnapshot(true, true, 11111, 0.64f, 62.5f, 0.29f, 0.31f),
            GearShift: new GearShiftEffectSnapshot(true, true, 6, 6, 4321u, 18.2f, 4, 0.52f),
            Kerb: new KerbEffectSnapshot(true, true, 7, "Rumble strip", 48.5f, 0.23f, 2, 0.28f),
            Impact: new ImpactEffectSnapshot(true, true, 9876u, 42.4f, 0.72f, 3, 0.44f),
            RoadTexture: new RoadTextureEffectSnapshot(
                true,
                false,
                true,
                0,
                "Asphalt",
                52.5f,
                0.33f,
                0.57f,
                0.37f,
                RoadTextureSignal.Inactive(DateTimeOffset.UnixEpoch) with
                {
                    OutputIntensity = 0.41f,
                    SpeedScale = 0.82f,
                    GearDuckingActive = true,
                    SpeedKph = 144,
                    NoiseAmount = 0.19f,
                    SurfaceName = "Asphalt",
                    SuppressedReason = null
                },
                0.18f),
            Slip: new SlipEffectSnapshot(
                true,
                false,
                true,
                true,
                0.15f,
                0.63f,
                0.11f,
                0.09f,
                0.78f,
                43.5f,
                0.24f,
                0.27f,
                "Wheel lock",
                "Front lock active",
                0.39f),
            ActiveEffectCount: 4,
            PeakLevel: 0.61f)
        {
            ActivityItems =
            [
                new HapticEffectActivityItem("engine", "active"),
                new HapticEffectActivityItem("road", "warming")
            ]
        };

        var options = new HapticEffectEngineOptions(
            Engine: EngineVibrationEffectOptions.Default with
            {
                IsEnabled = false,
                Gain = 0.46f,
                MinimumFrequencyHz = 21f,
                MaximumFrequencyHz = 88f
            },
            GearShift: GearShiftEffectOptions.Default with
            {
                IsEnabled = true,
                Gain = 0.64f,
                PulseFrequencyHz = 39f,
                PulseDuration = TimeSpan.FromMilliseconds(58)
            },
            Kerb: KerbEffectOptions.Default with
            {
                IsEnabled = true,
                Gain = 0.35f,
                BaseFrequencyHz = 24f,
                HighFrequencyHz = 61f
            },
            Impact: ImpactEffectOptions.Default with
            {
                IsEnabled = true,
                Gain = 0.41f,
                PulseFrequencyHz = 36f,
                PulseDuration = TimeSpan.FromMilliseconds(84)
            },
            RoadTexture: RoadTextureEffectOptions.Default with
            {
                IsEnabled = true,
                Bst1OutputEnabled = false,
                Gain = 0.44f,
                MinimumSpeedKph = 13f,
                FullIntensitySpeedKph = 231f,
                Bst1LowSpeedFrequencyHz = 27f,
                Bst1HighSpeedFrequencyHz = 67f,
                Bst1SpeedFrequencyInfluence = 0.72f,
                Bst1GrainAmount = 0.16f
            },
            Slip: SlipEffectOptions.Default with
            {
                WheelSlipEnabled = false,
                WheelSlipGain = 0.53f,
                WheelSlipFrequencyHz = 41f,
                WheelSlipNoiseAmount = 0.22f,
                SlipRatioThreshold = 0.17f,
                WheelLockEnabled = true,
                WheelLockGain = 0.47f,
                WheelLockFrequencyHz = 38f,
                WheelLockNoiseAmount = 0.21f,
                BrakeLockWheelSpeedRatioThreshold = 0.74f
            });

        var snapshot = EffectsStatusSnapshotBuilder.Build(runtime, options);

        Assert.False(snapshot.Engine.IsEnabled);
        Assert.Equal(11111f, snapshot.Engine.LastRpm);
        Assert.Equal(21d, snapshot.Engine.MinimumFrequencyHz);
        Assert.Equal(88d, snapshot.Engine.MaximumFrequencyHz);
        Assert.Equal("6", snapshot.GearShift.LastObservedGearText);
        Assert.Equal(58d, snapshot.GearShift.PulseDurationMs);
        Assert.Equal("Rumble strip", snapshot.Kerb.DominantSurfaceName);
        Assert.Equal(84d, snapshot.Impact.PulseDurationMs);
        Assert.True(snapshot.SharedRoadSignal.GearDuckingActive);
        Assert.False(snapshot.RoadTexture.Bst1OutputEnabled);
        Assert.Equal(231d, snapshot.RoadTexture.FullIntensitySpeedKph);
        Assert.True(snapshot.Slip.HasMeaningfulTelemetry);
        Assert.Equal("Wheel lock", snapshot.Slip.ActiveSource);
        Assert.False(snapshot.Slip.WheelSlipEnabled);
        Assert.True(snapshot.Slip.WheelLockEnabled);
        Assert.Same(runtime.ActivityItems, snapshot.ActivityItems);
    }

    [Fact]
    public void Build_CreatesOrderedSummaryItemsForFallbackStatus()
    {
        var snapshot = EffectsStatusSnapshotBuilder.Build(
            new HapticEffectEngineSnapshot(
                Engine: new EngineVibrationEffectSnapshot(false, false, null, 0f, 0f, 0f, 0f),
                GearShift: new GearShiftEffectSnapshot(true, true, 5, 5, 120u, 1.2f, 2, 0.2f),
                Kerb: new KerbEffectSnapshot(false, false, null, "None", 0f, 0f, 0, 0f),
                Impact: new ImpactEffectSnapshot(true, true, 500u, 2.5f, 0.7f, 3, 0.4f),
                RoadTexture: new RoadTextureEffectSnapshot(
                    false,
                    false,
                    false,
                    null,
                    "None",
                    0f,
                    0f,
                    0f,
                    0f,
                    RoadTextureSignal.Inactive(DateTimeOffset.UnixEpoch),
                    0f),
                Slip: new SlipEffectSnapshot(false, false, false, false, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, string.Empty, "Idle", 0f),
                ActiveEffectCount: 2,
                PeakLevel: 0.4f),
            HapticEffectEngineOptions.Default);

        Assert.Collection(
            snapshot.SummaryItems,
            item =>
            {
                Assert.Equal("engine", item.Key);
                Assert.Equal("engine idle", item.SummaryText);
            },
            item =>
            {
                Assert.Equal("gear", item.Key);
                Assert.Equal("gear pulse active", item.SummaryText);
            },
            item =>
            {
                Assert.Equal("kerb", item.Key);
                Assert.Equal("kerb idle", item.SummaryText);
            },
            item =>
            {
                Assert.Equal("impact", item.Key);
                Assert.Equal("impact pulse active", item.SummaryText);
            },
            item =>
            {
                Assert.Equal("road", item.Key);
                Assert.Equal("road idle", item.SummaryText);
            },
            item =>
            {
                Assert.Equal("slip", item.Key);
                Assert.Equal("slip idle", item.SummaryText);
            });
    }
}
