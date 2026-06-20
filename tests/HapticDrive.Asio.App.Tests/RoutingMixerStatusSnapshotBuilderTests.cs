using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.App.Tests;

public sealed class RoutingMixerStatusSnapshotBuilderTests
{
    [Fact]
    public void Build_MapsEffectAndRoutingInputsIntoStructuredSnapshot()
    {
        var effectSnapshot = new HapticEffectEngineSnapshot(
            Engine: new EngineVibrationEffectSnapshot(true, true, 11000, 0.7f, 62f, 0.3f, 0.31f),
            GearShift: new GearShiftEffectSnapshot(true, true, 6, 6, 420u, 12.1f, 2, 0.45f),
            Kerb: new KerbEffectSnapshot(true, false, 7, "Kerb", 45f, 0.1f, 1, 0.12f),
            Impact: new ImpactEffectSnapshot(true, true, 808u, 4.2f, 0.8f, 3, 0.33f),
            RoadTexture: new RoadTextureEffectSnapshot(true, true, true, 0, "Asphalt", 55f, 0.2f, 0.6f, 0.25f, HapticDrive.Asio.Core.Haptics.RoadTextureSignal.Inactive(DateTimeOffset.UnixEpoch) with { OutputIntensity = 0.3f, SuppressedReason = null }, 0.09f),
            Slip: new SlipEffectSnapshot(true, true, true, true, 0.15f, 0.44f, 0.08f, 0.06f, 0.77f, 42f, 0.19f, 0.2f, "Wheel lock", "Front lock", 0.29f),
            ActiveEffectCount: 5,
            PeakLevel: 0.52f)
        {
            ActivityItems =
            [
                new HapticEffectActivityItem("engine", "active"),
                new HapticEffectActivityItem("impact", "pulsing")
            ]
        };

        var snapshot = RoutingMixerStatusSnapshotBuilder.Build(new RoutingMixerStatusBuildInputs(
            MasterGain: 0.73,
            SafetyOutputGain: 0.92,
            EmergencyMuted: true,
            NormalMuted: false,
            OutputPeakLevel: 0.41f,
            MixerPeakLevel: 0.38f,
            LimitedSampleCount: 4,
            ClippedSampleCount: 1,
            SelectedOutputModeText: "ASIO",
            SelectedAsioDriverNameText: "Driver A",
            SelectedAsioOutputChannelText: "2",
            AsioArmed: true,
            TrueAsioStatusText: "ready",
            Bst1GearEnabled: true,
            EffectSnapshot: effectSnapshot,
            PhprPedalsModeText: "Direct",
            BrakeGearPulseEnabled: true,
            DirectReadinessText: "direct ready",
            DirectConnectionStateText: "Connected",
            BrakeGearActive: true,
            BrakeRoadEnabled: true,
            BrakeRoadActive: true,
            BrakeLockEnabled: true,
            BrakeLockActive: true,
            ThrottleGearPulseEnabled: false,
            PhprSoftwareCoexistenceStatusText: "Clean",
            RealEmergencyStopActive: false,
            ThrottleGearActive: false,
            ThrottleRoadEnabled: true,
            ThrottleRoadActive: true,
            ThrottleSlipEnabled: true,
            ThrottleSlipActive: false));

        Assert.Equal(0.73, snapshot.MasterGain);
        Assert.True(snapshot.Bst1GearActive);
        Assert.True(snapshot.Bst1RoadEnabled);
        Assert.True(snapshot.Bst1RoadActive);
        Assert.True(snapshot.EngineActive);
        Assert.True(snapshot.ImpactActive);
        Assert.True(snapshot.WheelLockActive);
        Assert.False(snapshot.WheelSlipActive);
        Assert.Equal(5, snapshot.ActiveEffectCount);
        Assert.Same(effectSnapshot.ActivityItems, snapshot.ActivityItems);
        Assert.Collection(
            snapshot.Bst1Effects,
            item => Assert.Equal("engine", item.Key),
            item => Assert.Equal("gear", item.Key),
            item => Assert.Equal("kerb", item.Key),
            item => Assert.Equal("impact", item.Key),
            item => Assert.Equal("road", item.Key),
            item => Assert.Equal("slip", item.Key),
            item => Assert.Equal("lock", item.Key));
    }
}
