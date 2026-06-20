using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.App.Tests;

public sealed class Bst1EffectSummarySnapshotBuilderTests
{
    [Fact]
    public void Build_MapsShippedBst1EffectStatesIntoOrderedSummaryItems()
    {
        var snapshot = Bst1EffectSummarySnapshotBuilder.Build(
            new HapticEffectEngineSnapshot(
                Engine: new EngineVibrationEffectSnapshot(true, false, 9000, 0.4f, 51f, 0.1f, 0.2f),
                GearShift: new GearShiftEffectSnapshot(true, true, 5, 5, 321u, 9.5f, 2, 0.4f),
                Kerb: new KerbEffectSnapshot(true, false, 7, "Rumble strip", 44f, 0.12f, 1, 0.1f),
                Impact: new ImpactEffectSnapshot(false, true, 999u, 8.2f, 0.7f, 3, 0.3f),
                RoadTexture: new RoadTextureEffectSnapshot(
                    true,
                    true,
                    true,
                    0,
                    "Asphalt",
                    58f,
                    0.2f,
                    0.5f,
                    0.25f,
                    RoadTextureSignal.Inactive(DateTimeOffset.UnixEpoch) with { OutputIntensity = 0.4f, SuppressedReason = null },
                    0.1f),
                Slip: new SlipEffectSnapshot(true, true, true, true, 0.33f, 0.22f, 0.11f, 0.07f, 0.81f, 43f, 0.2f, 0.18f, "Wheel slip", "Rear slip", 0.29f),
                ActiveEffectCount: 4,
                PeakLevel: 0.44f));

        Assert.True(snapshot.OverallSlipLockEnabled);
        Assert.Equal(0.44f, snapshot.PeakLevel);
        Assert.Collection(
            snapshot.Items,
            item => Assert.Equal(("engine", false), (item.Key, item.IsActive)),
            item => Assert.Equal(("gear", true), (item.Key, item.IsActive)),
            item => Assert.Equal(("kerb", false), (item.Key, item.IsActive)),
            item => Assert.Equal(("impact", true), (item.Key, item.IsActive)),
            item => Assert.Equal(("road", true), (item.Key, item.IsActive)),
            item => Assert.Equal(("slip", true), (item.Key, item.IsActive)),
            item => Assert.Equal(("lock", true), (item.Key, item.IsActive)));
    }
}
