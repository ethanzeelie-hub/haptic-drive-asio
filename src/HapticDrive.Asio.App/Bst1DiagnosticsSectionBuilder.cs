using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App;

internal sealed record Bst1DiagnosticsSectionInputs(
    HapticEffectEngineSnapshot EffectSnapshot,
    float MixerPeakLevel,
    float OutputPeakLevel,
    int LimitedSampleCount,
    int ClippedSampleCount,
    bool EmergencyMute);

internal sealed record Bst1DiagnosticsSectionSnapshot(
    Bst1EffectSummarySnapshot Effects,
    string SlipLockText,
    string MixerSafetyText);

internal static class Bst1DiagnosticsSectionBuilder
{
    public static Bst1DiagnosticsSectionSnapshot Build(Bst1DiagnosticsSectionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.EffectSnapshot);

        var effectSnapshot = inputs.EffectSnapshot;
        return new Bst1DiagnosticsSectionSnapshot(
            Effects: Bst1EffectSummarySnapshotBuilder.Build(effectSnapshot),
            SlipLockText: $"source {effectSnapshot.Slip.ActiveSource}; reason {effectSnapshot.Slip.ActiveReason}; slip intensity {effectSnapshot.Slip.CurrentSlipIntensity:0.00}; lock intensity {effectSnapshot.Slip.CurrentLockIntensity:0.00}; slip ratio {effectSnapshot.Slip.CurrentSlipRatio:0.00}; slip angle {effectSnapshot.Slip.CurrentSlipAngleRadians:0.00} rad; wheel-speed ratio {effectSnapshot.Slip.CurrentMinimumWheelSpeedRatio:0.00}; frequency {effectSnapshot.Slip.CurrentFrequencyHz:0.0} Hz; roughness {effectSnapshot.Slip.CurrentNoiseAmount:P0}; peak {effectSnapshot.Slip.PeakLevel:0.000}.",
            MixerSafetyText: $"mixer peak {inputs.MixerPeakLevel:0.000}; output peak {inputs.OutputPeakLevel:0.000}; limited {inputs.LimitedSampleCount:N0}; clipped {inputs.ClippedSampleCount:N0}; emergency mute {inputs.EmergencyMute}.");
    }
}
