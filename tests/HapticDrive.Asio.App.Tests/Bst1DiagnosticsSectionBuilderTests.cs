using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.App.Tests;

public sealed class Bst1DiagnosticsSectionBuilderTests
{
    [Fact]
    public void Build_CreatesBst1SummarySlipLockAndMixerSafetyText()
    {
        var section = Bst1DiagnosticsSectionBuilder.Build(new Bst1DiagnosticsSectionInputs(
            EffectSnapshot: new HapticEffectEngineSnapshot(
                Engine: new EngineVibrationEffectSnapshot(true, true, 10200, 0.55f, 58f, 0.2f, 0.24f),
                GearShift: new GearShiftEffectSnapshot(true, false, 5, 5, 120u, 6.5f, 0, 0.1f),
                Kerb: new KerbEffectSnapshot(true, false, 7, "Kerb", 40f, 0.1f, 1, 0.09f),
                Impact: new ImpactEffectSnapshot(true, false, 450u, 8.3f, 0.2f, 0, 0.08f),
                RoadTexture: new RoadTextureEffectSnapshot(true, true, true, 0, "Asphalt", 49f, 0.15f, 0.4f, 0.12f, RoadTextureSignal.Inactive(DateTimeOffset.UnixEpoch) with { OutputIntensity = 0.22f, SuppressedReason = null }, 0.06f),
                Slip: new SlipEffectSnapshot(true, true, true, true, 0.31f, 0.14f, 0.11f, 0.07f, 0.84f, 43f, 0.19f, 0.18f, "Wheel slip", "Rear slip active", 0.22f),
                ActiveEffectCount: 3,
                PeakLevel: 0.28f),
            MixerPeakLevel: 0.33f,
            OutputPeakLevel: 0.27f,
            LimitedSampleCount: 4,
            ClippedSampleCount: 1,
            EmergencyMute: true));

        Assert.Equal(0.28f, section.Effects.PeakLevel);
        Assert.Contains("source Wheel slip; reason Rear slip active;", section.SlipLockText, StringComparison.Ordinal);
        Assert.Contains("slip intensity 0.31; lock intensity 0.14;", section.SlipLockText, StringComparison.Ordinal);
        Assert.Equal("mixer peak 0.330; output peak 0.270; limited 4; clipped 1; emergency mute True.", section.MixerSafetyText);
    }
}
