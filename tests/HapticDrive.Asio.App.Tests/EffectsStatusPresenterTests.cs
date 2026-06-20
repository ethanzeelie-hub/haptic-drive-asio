using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App.Tests;

public sealed class EffectsStatusPresenterTests
{
    [Fact]
    public void Build_WhenEffectsAreIdle_ShowsWaitingGuidance()
    {
        var presentation = EffectsStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("Idle", presentation.EngineEffectStateText);
        Assert.Equal("Waiting for RPM telemetry.", presentation.EngineEffectDetailText);
        Assert.Equal("Idle", presentation.GearShiftEffectStateText);
        Assert.Equal("Waiting for gear telemetry.", presentation.GearShiftEffectDetailText);
        Assert.Equal("Idle", presentation.RoadTextureEffectStateText);
        Assert.Equal("Waiting for speed and surface telemetry.", presentation.RoadTextureEffectDetailText);
        Assert.Equal("Idle", presentation.SlipEffectStateText);
        Assert.Equal("Waiting for Motion Ex slip ratio / angle and wheel-speed telemetry.", presentation.SlipEffectDetailText);
        Assert.Equal("0 active effect source(s); engine idle, gear idle, kerb idle, impact idle, road idle, slip idle; peak 0.000.", presentation.EffectsPageStatusText);
    }

    [Fact]
    public void Build_WhenEffectsAreActive_ShowsLiveSummaries()
    {
        var presentation = EffectsStatusPresenter.Build(CreateSnapshot(
            sharedRoadSignal: new SharedRoadSignalStatusSnapshot(true, 0.42f, 0.88f, true),
            engine: new EngineEffectStatusSnapshot(true, 11450f, 61.5f, 0.31f, 0.45, 20, 90, true),
            gearShift: new GearShiftEffectStatusSnapshot(true, "5", 1234, 0.52f, 0.65, 37, 55, true),
            kerb: new KerbEffectStatusSnapshot(true, "Rumble strip", 2, 47.5f, 0.28f, 0.35, 28, 63, true),
            impact: new ImpactEffectStatusSnapshot(true, 9876, 0.72f, 0.44f, 0.40, 36, 80, true),
            roadTexture: new RoadTextureEffectStatusSnapshot(true, "Asphalt", true, 0.56f, 143, 52.5f, 0.18, 0.37f, 0.43, 12, 220, 28, 64, 0.70, 0.18, true, true),
            slip: new SlipEffectStatusSnapshot(true, "Wheel slip", true, "Rear slip active", 0.61f, 0.18f, 0.09f, 0.11f, 0.84f, 44.5f, 0.24, 0.39f, 0.52, 42, 0.25, 0.15, true, 0.40, 38, 0.20, 0.75, true),
            activeEffectCount: 4,
            peakLevel: 0.52f));

        Assert.Equal("Active", presentation.EngineEffectStateText);
        Assert.Contains("11,450 RPM -> 61.5 Hz, peak 0.310.", presentation.EngineEffectDetailText, StringComparison.Ordinal);
        Assert.Equal("Pulse active", presentation.GearShiftEffectStateText);
        Assert.Contains("Last gear 5; last shift frame 1,234; peak 0.520.", presentation.GearShiftEffectDetailText, StringComparison.Ordinal);
        Assert.Equal("BST-1 active", presentation.RoadTextureEffectStateText);
        Assert.Contains("Asphalt; shared signal active; mix 0.56;", presentation.RoadTextureEffectDetailText, StringComparison.Ordinal);
        Assert.Contains("speed 143", presentation.RoadTextureEffectDetailText, StringComparison.Ordinal);
        Assert.Contains("52.5 Hz;", presentation.RoadTextureEffectDetailText, StringComparison.Ordinal);
        Assert.Contains("BST-1 peak 0.370.", presentation.RoadTextureEffectDetailText, StringComparison.Ordinal);
        Assert.Equal("Wheel slip active", presentation.SlipEffectStateText);
        Assert.Contains("Rear slip active; slip 0.61", presentation.SlipEffectDetailText, StringComparison.Ordinal);
        Assert.Equal("4 active effect source(s); engine active, gear pulse active, kerb active, impact pulse active, road bst-1 active, slip wheel slip active; peak 0.520.", presentation.EffectsPageStatusText);
    }

    [Fact]
    public void Build_WhenGenericActivityItemsExist_UsesThemForStatusSummary()
    {
        var presentation = EffectsStatusPresenter.Build(CreateSnapshot(
            activeEffectCount: 2,
            peakLevel: 0.21f) with
        {
            ActivityItems =
            [
                new HapticEffectActivityItem("engine", "active"),
                new HapticEffectActivityItem("new effect", "warming up")
            ]
        });

        Assert.Equal("2 active effect source(s); engine active, new effect warming up; peak 0.210.", presentation.EffectsPageStatusText);
    }

    private static EffectsStatusSnapshot CreateSnapshot(
        SharedRoadSignalStatusSnapshot? sharedRoadSignal = null,
        EngineEffectStatusSnapshot? engine = null,
        GearShiftEffectStatusSnapshot? gearShift = null,
        KerbEffectStatusSnapshot? kerb = null,
        ImpactEffectStatusSnapshot? impact = null,
        RoadTextureEffectStatusSnapshot? roadTexture = null,
        SlipEffectStatusSnapshot? slip = null,
        int activeEffectCount = 0,
        float peakLevel = 0f)
    {
        return new EffectsStatusSnapshot(
            SharedRoadSignal: sharedRoadSignal ?? new SharedRoadSignalStatusSnapshot(false, 0f, 0f, false),
            Engine: engine ?? new EngineEffectStatusSnapshot(false, null, 0f, 0f, 0.35, 20, 80, true),
            GearShift: gearShift ?? new GearShiftEffectStatusSnapshot(false, null, null, 0f, 0.50, 35, 40, true),
            Kerb: kerb ?? new KerbEffectStatusSnapshot(false, null, 0, 0f, 0f, 0.30, 24, 58, true),
            Impact: impact ?? new ImpactEffectStatusSnapshot(false, null, 0f, 0f, 0.30, 40, 60, true),
            RoadTexture: roadTexture ?? new RoadTextureEffectStatusSnapshot(false, null, false, 0f, 0f, 0f, 0, 0f, 0.35, 10, 180, 28, 60, 0.50, 0.10, false, true),
            Slip: slip ?? new SlipEffectStatusSnapshot(false, "Wheel slip", false, "Idle", 0f, 0f, 0f, 0f, 1f, 0f, 0, 0f, 0.35, 35, 0.10, 0.08, true, 0.30, 32, 0.08, 0.70, true),
            ActiveEffectCount: activeEffectCount,
            PeakLevel: peakLevel);
    }
}
