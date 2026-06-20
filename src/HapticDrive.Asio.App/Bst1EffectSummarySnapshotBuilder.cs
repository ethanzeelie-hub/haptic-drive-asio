using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal static class Bst1EffectSummarySnapshotBuilder
{
    public static Bst1EffectSummarySnapshot Build(HapticEffectEngineSnapshot effectSnapshot)
    {
        ArgumentNullException.ThrowIfNull(effectSnapshot);

        return new Bst1EffectSummarySnapshot(
            [
                new Bst1EffectSummaryItem("engine", "engine", effectSnapshot.Engine.IsEnabled, effectSnapshot.Engine.IsActive),
                new Bst1EffectSummaryItem("gear", "gear", effectSnapshot.GearShift.IsEnabled, effectSnapshot.GearShift.IsActive),
                new Bst1EffectSummaryItem("kerb", "kerb", effectSnapshot.Kerb.IsEnabled, effectSnapshot.Kerb.IsActive),
                new Bst1EffectSummaryItem("impact", "impact", effectSnapshot.Impact.IsEnabled, effectSnapshot.Impact.IsActive),
                new Bst1EffectSummaryItem("road", "road", effectSnapshot.RoadTexture.Bst1OutputEnabled, effectSnapshot.RoadTexture.IsActive),
                new Bst1EffectSummaryItem("slip", "slip", effectSnapshot.Slip.WheelSlipEnabled, effectSnapshot.Slip.IsActive && effectSnapshot.Slip.CurrentSlipIntensity > 0f),
                new Bst1EffectSummaryItem("lock", "lock", effectSnapshot.Slip.WheelLockEnabled, effectSnapshot.Slip.IsActive && effectSnapshot.Slip.CurrentLockIntensity > 0f)
            ],
            effectSnapshot.Slip.IsEnabled,
            effectSnapshot.PeakLevel);
    }
}
