using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal static class Bst1EffectSummarySnapshotBuilder
{
    public static Bst1EffectSummarySnapshot Build(HapticEffectEngineSnapshot effectSnapshot)
    {
        ArgumentNullException.ThrowIfNull(effectSnapshot);

        return new Bst1EffectSummarySnapshot(
            [
                BuildItem(Bst1EffectCatalog.GetRequired("engine"), effectSnapshot.Engine.IsEnabled, effectSnapshot.Engine.IsActive),
                BuildItem(Bst1EffectCatalog.GetRequired("gear"), effectSnapshot.GearShift.IsEnabled, effectSnapshot.GearShift.IsActive),
                BuildItem(Bst1EffectCatalog.GetRequired("kerb"), effectSnapshot.Kerb.IsEnabled, effectSnapshot.Kerb.IsActive),
                BuildItem(Bst1EffectCatalog.GetRequired("impact"), effectSnapshot.Impact.IsEnabled, effectSnapshot.Impact.IsActive),
                BuildItem(Bst1EffectCatalog.GetRequired("road"), effectSnapshot.RoadTexture.Bst1OutputEnabled, effectSnapshot.RoadTexture.IsActive),
                BuildItem(Bst1EffectCatalog.GetRequired("slip"), effectSnapshot.Slip.WheelSlipEnabled, effectSnapshot.Slip.IsActive && effectSnapshot.Slip.CurrentSlipIntensity > 0f),
                BuildItem(Bst1EffectCatalog.GetRequired("lock"), effectSnapshot.Slip.WheelLockEnabled, effectSnapshot.Slip.IsActive && effectSnapshot.Slip.CurrentLockIntensity > 0f)
            ],
            effectSnapshot.Slip.IsEnabled,
            effectSnapshot.PeakLevel);
    }

    private static Bst1EffectSummaryItem BuildItem(
        Bst1EffectCatalogItem descriptor,
        bool isEnabled,
        bool isActive)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new Bst1EffectSummaryItem(descriptor.Key, descriptor.DisplayName, isEnabled, isActive);
    }
}
