using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal static class EffectsStatusSnapshotBuilder
{
    public static EffectsStatusSnapshot Build(
        HapticEffectEngineSnapshot snapshot,
        HapticEffectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        return new EffectsStatusSnapshot(
            SharedRoadSignal: new SharedRoadSignalStatusSnapshot(
                IsEnabled: options.RoadTexture.IsEnabled,
                OutputIntensity: snapshot.RoadTexture.Signal.OutputIntensity,
                SpeedScale: snapshot.RoadTexture.Signal.SpeedScale,
                GearDuckingActive: snapshot.RoadTexture.Signal.GearDuckingActive),
            Engine: new EngineEffectStatusSnapshot(
                IsActive: snapshot.Engine.IsActive,
                LastRpm: snapshot.Engine.LastRpm,
                CurrentFrequencyHz: snapshot.Engine.CurrentFrequencyHz,
                PeakLevel: snapshot.Engine.PeakLevel,
                Gain: options.Engine.Gain,
                MinimumFrequencyHz: options.Engine.MinimumFrequencyHz,
                MaximumFrequencyHz: options.Engine.MaximumFrequencyHz,
                IsEnabled: options.Engine.IsEnabled),
            GearShift: new GearShiftEffectStatusSnapshot(
                IsActive: snapshot.GearShift.IsActive,
                LastObservedGearText: snapshot.GearShift.LastObservedGear?.ToString(),
                LastShiftFrameIdentifier: snapshot.GearShift.LastShiftFrameIdentifier,
                PeakLevel: snapshot.GearShift.PeakLevel,
                Gain: options.GearShift.Gain,
                PulseFrequencyHz: options.GearShift.PulseFrequencyHz,
                PulseDurationMs: options.GearShift.PulseDuration.TotalMilliseconds,
                IsEnabled: options.GearShift.IsEnabled),
            Kerb: new KerbEffectStatusSnapshot(
                IsActive: snapshot.Kerb.IsActive,
                DominantSurfaceName: snapshot.Kerb.DominantSurfaceTypeId is null ? null : snapshot.Kerb.DominantSurfaceName,
                ActiveWheelCount: snapshot.Kerb.ActiveWheelCount,
                CurrentFrequencyHz: snapshot.Kerb.CurrentFrequencyHz,
                PeakLevel: snapshot.Kerb.PeakLevel,
                Gain: options.Kerb.Gain,
                BaseFrequencyHz: options.Kerb.BaseFrequencyHz,
                HighFrequencyHz: options.Kerb.HighFrequencyHz,
                IsEnabled: options.Kerb.IsEnabled),
            Impact: new ImpactEffectStatusSnapshot(
                IsActive: snapshot.Impact.IsActive,
                LastImpactFrameIdentifier: snapshot.Impact.LastImpactFrameIdentifier,
                CurrentIntensity: snapshot.Impact.CurrentIntensity,
                PeakLevel: snapshot.Impact.PeakLevel,
                Gain: options.Impact.Gain,
                PulseFrequencyHz: options.Impact.PulseFrequencyHz,
                PulseDurationMs: options.Impact.PulseDuration.TotalMilliseconds,
                IsEnabled: options.Impact.IsEnabled),
            RoadTexture: new RoadTextureEffectStatusSnapshot(
                IsActive: snapshot.RoadTexture.IsActive,
                DominantSurfaceName: snapshot.RoadTexture.DominantSurfaceTypeId is null ? null : snapshot.RoadTexture.DominantSurfaceName,
                SharedSignalIsActive: snapshot.RoadTexture.Signal.IsActive,
                SurfaceMix: snapshot.RoadTexture.SurfaceMix,
                SpeedKph: snapshot.RoadTexture.Signal.SpeedKph,
                CurrentFrequencyHz: snapshot.RoadTexture.CurrentFrequencyHz,
                NoiseAmount: snapshot.RoadTexture.Signal.NoiseAmount,
                PeakLevel: snapshot.RoadTexture.PeakLevel,
                Gain: options.RoadTexture.Gain,
                MinimumSpeedKph: options.RoadTexture.MinimumSpeedKph,
                FullIntensitySpeedKph: options.RoadTexture.FullIntensitySpeedKph,
                LowSpeedFrequencyHz: options.RoadTexture.Bst1LowSpeedFrequencyHz,
                HighSpeedFrequencyHz: options.RoadTexture.Bst1HighSpeedFrequencyHz,
                SpeedFrequencyInfluence: options.RoadTexture.Bst1SpeedFrequencyInfluence,
                GrainAmount: options.RoadTexture.Bst1GrainAmount,
                SharedSignalEnabled: options.RoadTexture.IsEnabled,
                Bst1OutputEnabled: options.RoadTexture.Bst1OutputEnabled),
            Slip: new SlipEffectStatusSnapshot(
                IsActive: snapshot.Slip.IsActive,
                ActiveSource: snapshot.Slip.ActiveSource,
                HasMeaningfulTelemetry: HasMeaningfulSlipTelemetry(snapshot.Slip),
                ActiveReason: snapshot.Slip.ActiveReason,
                CurrentSlipIntensity: snapshot.Slip.CurrentSlipIntensity,
                CurrentSlipRatio: snapshot.Slip.CurrentSlipRatio,
                CurrentSlipAngleRadians: snapshot.Slip.CurrentSlipAngleRadians,
                CurrentLockIntensity: snapshot.Slip.CurrentLockIntensity,
                CurrentMinimumWheelSpeedRatio: snapshot.Slip.CurrentMinimumWheelSpeedRatio,
                CurrentFrequencyHz: snapshot.Slip.CurrentFrequencyHz,
                CurrentNoiseAmount: snapshot.Slip.CurrentNoiseAmount,
                PeakLevel: snapshot.Slip.PeakLevel,
                WheelSlipGain: options.Slip.WheelSlipGain,
                WheelSlipFrequencyHz: options.Slip.WheelSlipFrequencyHz,
                WheelSlipNoiseAmount: options.Slip.WheelSlipNoiseAmount,
                SlipRatioThreshold: options.Slip.SlipRatioThreshold,
                WheelSlipEnabled: options.Slip.WheelSlipEnabled,
                WheelLockGain: options.Slip.WheelLockGain,
                WheelLockFrequencyHz: options.Slip.WheelLockFrequencyHz,
                WheelLockNoiseAmount: options.Slip.WheelLockNoiseAmount,
                BrakeLockWheelSpeedRatioThreshold: options.Slip.BrakeLockWheelSpeedRatioThreshold,
                WheelLockEnabled: options.Slip.WheelLockEnabled),
            ActiveEffectCount: snapshot.ActiveEffectCount,
            PeakLevel: snapshot.PeakLevel)
        {
            ActivityItems = snapshot.ActivityItems,
            SummaryItems = BuildSummaryItems(snapshot)
        };
    }

    private static bool HasMeaningfulSlipTelemetry(SlipEffectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.CurrentSlipRatio > 0f
            || snapshot.CurrentSlipAngleRadians > 0f
            || Math.Abs(snapshot.CurrentMinimumWheelSpeedRatio - 1f) >= 0.0001f;
    }

    private static IReadOnlyList<EffectStatusSummaryItem> BuildSummaryItems(HapticEffectEngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return
        [
            new EffectStatusSummaryItem("engine", snapshot.Engine.IsActive ? "engine active" : "engine idle"),
            new EffectStatusSummaryItem("gear", snapshot.GearShift.IsActive ? "gear pulse active" : "gear idle"),
            new EffectStatusSummaryItem("kerb", snapshot.Kerb.IsActive ? "kerb active" : "kerb idle"),
            new EffectStatusSummaryItem("impact", snapshot.Impact.IsActive ? "impact pulse active" : "impact idle"),
            new EffectStatusSummaryItem("road", snapshot.RoadTexture.IsActive ? "road bst-1 active" : "road idle"),
            new EffectStatusSummaryItem(
                "slip",
                snapshot.Slip.IsActive
                    ? $"{snapshot.Slip.ActiveSource?.ToLowerInvariant() ?? "slip"} active"
                    : "slip idle")
        ];
    }
}
