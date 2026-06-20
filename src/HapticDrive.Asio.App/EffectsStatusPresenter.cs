using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal sealed record SharedRoadSignalStatusSnapshot(
    bool IsEnabled,
    float OutputIntensity,
    float SpeedScale,
    bool GearDuckingActive);

internal sealed record EngineEffectStatusSnapshot(
    bool IsActive,
    float? LastRpm,
    float CurrentFrequencyHz,
    float PeakLevel,
    double Gain,
    double MinimumFrequencyHz,
    double MaximumFrequencyHz,
    bool IsEnabled);

internal sealed record GearShiftEffectStatusSnapshot(
    bool IsActive,
    string? LastObservedGearText,
    uint? LastShiftFrameIdentifier,
    float PeakLevel,
    double Gain,
    double PulseFrequencyHz,
    double PulseDurationMs,
    bool IsEnabled);

internal sealed record KerbEffectStatusSnapshot(
    bool IsActive,
    string? DominantSurfaceName,
    int ActiveWheelCount,
    float CurrentFrequencyHz,
    float PeakLevel,
    double Gain,
    double BaseFrequencyHz,
    double HighFrequencyHz,
    bool IsEnabled);

internal sealed record ImpactEffectStatusSnapshot(
    bool IsActive,
    uint? LastImpactFrameIdentifier,
    float CurrentIntensity,
    float PeakLevel,
    double Gain,
    double PulseFrequencyHz,
    double PulseDurationMs,
    bool IsEnabled);

internal sealed record RoadTextureEffectStatusSnapshot(
    bool IsActive,
    string? DominantSurfaceName,
    bool SharedSignalIsActive,
    float SurfaceMix,
    float SpeedKph,
    float CurrentFrequencyHz,
    double NoiseAmount,
    float PeakLevel,
    double Gain,
    double MinimumSpeedKph,
    double FullIntensitySpeedKph,
    double LowSpeedFrequencyHz,
    double HighSpeedFrequencyHz,
    double SpeedFrequencyInfluence,
    double GrainAmount,
    bool SharedSignalEnabled,
    bool Bst1OutputEnabled);

internal sealed record SlipEffectStatusSnapshot(
    bool IsActive,
    string? ActiveSource,
    bool HasMeaningfulTelemetry,
    string ActiveReason,
    float CurrentSlipIntensity,
    float CurrentSlipRatio,
    float CurrentSlipAngleRadians,
    float CurrentLockIntensity,
    float CurrentMinimumWheelSpeedRatio,
    float CurrentFrequencyHz,
    double CurrentNoiseAmount,
    float PeakLevel,
    double WheelSlipGain,
    double WheelSlipFrequencyHz,
    double WheelSlipNoiseAmount,
    double SlipRatioThreshold,
    bool WheelSlipEnabled,
    double WheelLockGain,
    double WheelLockFrequencyHz,
    double WheelLockNoiseAmount,
    double BrakeLockWheelSpeedRatioThreshold,
    bool WheelLockEnabled);

internal sealed record EffectsStatusSnapshot(
    SharedRoadSignalStatusSnapshot SharedRoadSignal,
    EngineEffectStatusSnapshot Engine,
    GearShiftEffectStatusSnapshot GearShift,
    KerbEffectStatusSnapshot Kerb,
    ImpactEffectStatusSnapshot Impact,
    RoadTextureEffectStatusSnapshot RoadTexture,
    SlipEffectStatusSnapshot Slip,
    int ActiveEffectCount,
    float PeakLevel)
{
    public IReadOnlyList<HapticEffectActivityItem> ActivityItems { get; init; } = [];
}

internal sealed record EffectsStatusPresentation(
    string SharedRoadSignalStatusText,
    string EngineEffectStateText,
    string EngineEffectDetailText,
    string EngineEffectDefaultsText,
    string GearShiftEffectStateText,
    string GearShiftEffectDetailText,
    string GearShiftEffectDefaultsText,
    string KerbEffectStateText,
    string KerbEffectDetailText,
    string KerbEffectDefaultsText,
    string ImpactEffectStateText,
    string ImpactEffectDetailText,
    string ImpactEffectDefaultsText,
    string RoadTextureEffectStateText,
    string RoadTextureEffectDetailText,
    string RoadTextureEffectDefaultsText,
    string SlipEffectStateText,
    string SlipEffectDetailText,
    string SlipEffectDefaultsText,
    string EffectsPageStatusText);

internal static class EffectsStatusPresenter
{
    public static EffectsStatusPresentation Build(EffectsStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var engineStateText = snapshot.Engine.IsActive ? "Active" : "Idle";
        var engineDetailText = snapshot.Engine.LastRpm is null
            ? "Waiting for RPM telemetry."
            : $"{snapshot.Engine.LastRpm:N0} RPM -> {snapshot.Engine.CurrentFrequencyHz:0.0} Hz, peak {snapshot.Engine.PeakLevel:0.000}.";
        var engineDefaultsText = $"Tuned gain {snapshot.Engine.Gain:P0}; base {snapshot.Engine.MinimumFrequencyHz:0}-{snapshot.Engine.MaximumFrequencyHz:0} Hz; enabled {snapshot.Engine.IsEnabled}.";

        var gearShiftStateText = snapshot.GearShift.IsActive ? "Pulse active" : "Idle";
        var gearShiftDetailText = string.IsNullOrWhiteSpace(snapshot.GearShift.LastObservedGearText)
            ? "Waiting for gear telemetry."
            : $"Last gear {snapshot.GearShift.LastObservedGearText}; last shift frame {FormatFrame(snapshot.GearShift.LastShiftFrameIdentifier)}; peak {snapshot.GearShift.PeakLevel:0.000}.";
        var gearShiftDefaultsText = $"Tuned gain {snapshot.GearShift.Gain:P0}; {snapshot.GearShift.PulseFrequencyHz:0} Hz pulse; {snapshot.GearShift.PulseDurationMs:0} ms; enabled {snapshot.GearShift.IsEnabled}.";

        var kerbStateText = snapshot.Kerb.IsActive ? "Active" : "Idle";
        var kerbDetailText = string.IsNullOrWhiteSpace(snapshot.Kerb.DominantSurfaceName)
            ? "Waiting for rumble strip / ridged surface telemetry."
            : $"{snapshot.Kerb.DominantSurfaceName}; {snapshot.Kerb.ActiveWheelCount} wheel(s); {snapshot.Kerb.CurrentFrequencyHz:0.0} Hz; peak {snapshot.Kerb.PeakLevel:0.000}.";
        var kerbDefaultsText = $"Tuned gain {snapshot.Kerb.Gain:P0}; {snapshot.Kerb.BaseFrequencyHz:0} Hz + {snapshot.Kerb.HighFrequencyHz:0} Hz; enabled {snapshot.Kerb.IsEnabled}.";

        var impactStateText = snapshot.Impact.IsActive ? "Pulse active" : "Idle";
        var impactDetailText = snapshot.Impact.LastImpactFrameIdentifier is null
            ? "Waiting for collision, vertical-G, force, or suspension spikes."
            : $"Last impact frame {snapshot.Impact.LastImpactFrameIdentifier:N0}; intensity {snapshot.Impact.CurrentIntensity:0.00}; peak {snapshot.Impact.PeakLevel:0.000}.";
        var impactDefaultsText = $"Tuned gain {snapshot.Impact.Gain:P0}; {snapshot.Impact.PulseFrequencyHz:0} Hz; {snapshot.Impact.PulseDurationMs:0} ms; enabled {snapshot.Impact.IsEnabled}.";

        var roadTextureStateText = snapshot.RoadTexture.IsActive ? "BST-1 active" : "Idle";
        var roadTextureDetailText = string.IsNullOrWhiteSpace(snapshot.RoadTexture.DominantSurfaceName)
            ? "Waiting for speed and surface telemetry."
            : $"{snapshot.RoadTexture.DominantSurfaceName}; shared signal {(snapshot.RoadTexture.SharedSignalIsActive ? "active" : "idle")}; mix {snapshot.RoadTexture.SurfaceMix:0.00}; speed {snapshot.RoadTexture.SpeedKph} km/h; {snapshot.RoadTexture.CurrentFrequencyHz:0.0} Hz; grain {snapshot.RoadTexture.NoiseAmount:P0}; BST-1 peak {snapshot.RoadTexture.PeakLevel:0.000}.";
        var roadTextureDefaultsText = $"BST-1 / ASIO road output gain {snapshot.RoadTexture.Gain:P0}; min {snapshot.RoadTexture.MinimumSpeedKph:0} km/h; speed ref {snapshot.RoadTexture.FullIntensitySpeedKph:0} km/h; {snapshot.RoadTexture.LowSpeedFrequencyHz:0}-{snapshot.RoadTexture.HighSpeedFrequencyHz:0} Hz; speed influence {snapshot.RoadTexture.SpeedFrequencyInfluence:P0}; grain {snapshot.RoadTexture.GrainAmount:P0}; shared signal {snapshot.RoadTexture.SharedSignalEnabled}; BST-1 output {snapshot.RoadTexture.Bst1OutputEnabled}.";

        var slipStateText = snapshot.Slip.IsActive
            ? $"{snapshot.Slip.ActiveSource} active"
            : "Idle";
        var slipDetailText = snapshot.Slip.HasMeaningfulTelemetry
            ? $"{snapshot.Slip.ActiveReason}; slip {snapshot.Slip.CurrentSlipIntensity:0.00} (ratio {snapshot.Slip.CurrentSlipRatio:0.00}, angle {snapshot.Slip.CurrentSlipAngleRadians:0.00} rad); lock {snapshot.Slip.CurrentLockIntensity:0.00} (wheel-speed ratio {snapshot.Slip.CurrentMinimumWheelSpeedRatio:0.00}); {snapshot.Slip.CurrentFrequencyHz:0.0} Hz; roughness {snapshot.Slip.CurrentNoiseAmount:P0}; peak {snapshot.Slip.PeakLevel:0.000}."
            : "Waiting for Motion Ex slip ratio / angle and wheel-speed telemetry.";
        var slipDefaultsText = $"Slip {snapshot.Slip.WheelSlipGain:P0} @ {snapshot.Slip.WheelSlipFrequencyHz:0} Hz, roughness {snapshot.Slip.WheelSlipNoiseAmount:P0}, threshold {snapshot.Slip.SlipRatioThreshold:0.00}, enabled {snapshot.Slip.WheelSlipEnabled}; lock {snapshot.Slip.WheelLockGain:P0} @ {snapshot.Slip.WheelLockFrequencyHz:0} Hz, roughness {snapshot.Slip.WheelLockNoiseAmount:P0}, sensitivity {snapshot.Slip.BrakeLockWheelSpeedRatioThreshold:0.00}, enabled {snapshot.Slip.WheelLockEnabled}.";
        var activitySummary = EffectActivitySummaryFormatter.Format(
            snapshot.ActivityItems,
            ", ",
            $"engine {engineStateText.ToLowerInvariant()}, gear {gearShiftStateText.ToLowerInvariant()}, kerb {kerbStateText.ToLowerInvariant()}, impact {impactStateText.ToLowerInvariant()}, road {roadTextureStateText.ToLowerInvariant()}, slip {slipStateText.ToLowerInvariant()}");

        return new EffectsStatusPresentation(
            SharedRoadSignalStatusText: $"Shared road signal {(snapshot.SharedRoadSignal.IsEnabled ? "enabled" : "disabled")}; output {snapshot.SharedRoadSignal.OutputIntensity:0.000}; speed scale {snapshot.SharedRoadSignal.SpeedScale:0.000}; gear ducking {snapshot.SharedRoadSignal.GearDuckingActive}.",
            EngineEffectStateText: engineStateText,
            EngineEffectDetailText: engineDetailText,
            EngineEffectDefaultsText: engineDefaultsText,
            GearShiftEffectStateText: gearShiftStateText,
            GearShiftEffectDetailText: gearShiftDetailText,
            GearShiftEffectDefaultsText: gearShiftDefaultsText,
            KerbEffectStateText: kerbStateText,
            KerbEffectDetailText: kerbDetailText,
            KerbEffectDefaultsText: kerbDefaultsText,
            ImpactEffectStateText: impactStateText,
            ImpactEffectDetailText: impactDetailText,
            ImpactEffectDefaultsText: impactDefaultsText,
            RoadTextureEffectStateText: roadTextureStateText,
            RoadTextureEffectDetailText: roadTextureDetailText,
            RoadTextureEffectDefaultsText: roadTextureDefaultsText,
            SlipEffectStateText: slipStateText,
            SlipEffectDetailText: slipDetailText,
            SlipEffectDefaultsText: slipDefaultsText,
            EffectsPageStatusText: $"{snapshot.ActiveEffectCount} active effect source(s); {activitySummary}; peak {snapshot.PeakLevel:0.000}.");
    }

    private static string FormatFrame(uint? frameIdentifier)
    {
        return frameIdentifier?.ToString("N0") ?? "none";
    }
}
