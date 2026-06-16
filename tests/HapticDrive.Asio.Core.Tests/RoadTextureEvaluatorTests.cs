using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Core.Tests;

public sealed class RoadTextureEvaluatorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DisabledRoadEmitsZeroSignal()
    {
        var evaluator = new RoadTextureEvaluator(RoadTextureEvaluatorOptions.Default with { IsEnabled = false });

        var signal = evaluator.Evaluate(State(speedKph: 140, surfaceType: 1), Context());

        Assert.False(signal.IsActive);
        Assert.Equal(0f, signal.OutputIntensity);
        Assert.Equal("road disabled", signal.SuppressedReason);
    }

    [Fact]
    public void StaleTelemetryEmitsZeroSignal()
    {
        var evaluator = new RoadTextureEvaluator();

        var signal = evaluator.Evaluate(State(speedKph: 140, surfaceType: 1), Context(telemetryStale: true));

        Assert.False(signal.IsActive);
        Assert.False(signal.TelemetryFresh);
        Assert.Equal("telemetry stale", signal.SuppressedReason);
        Assert.Equal(1, evaluator.StaleTelemetrySuppressedCount);
    }

    [Fact]
    public void DrivingNotArmedBlocksUnlessExplicitlyAllowed()
    {
        var evaluator = new RoadTextureEvaluator();

        var blocked = evaluator.Evaluate(State(speedKph: 140, surfaceType: 1), Context(drivingArmed: false));
        var allowed = evaluator.Evaluate(
            State(speedKph: 140, surfaceType: 1, frame: 2),
            Context(drivingArmed: false, allowWhenDrivingNotArmed: true));

        Assert.False(blocked.IsActive);
        Assert.Equal("driving not armed", blocked.SuppressedReason);
        Assert.True(allowed.IsActive);
    }

    [Fact]
    public void LowSpeedSmoothTrackProducesNearZeroSignal()
    {
        var evaluator = new RoadTextureEvaluator();

        var signal = evaluator.Evaluate(State(speedKph: 8, surfaceType: 0), Context());

        Assert.InRange(signal.OutputIntensity, 0f, 0.01f);
    }

    [Fact]
    public void SmoothTarmacAboveSpeedGateProducesSmallNonZeroDiagnosticSignal()
    {
        var evaluator = new RoadTextureEvaluator();

        var signal = evaluator.Evaluate(State(speedKph: 120, surfaceType: 0), Context());

        Assert.True(signal.IsActive);
        Assert.InRange(signal.RawIntensity, 0f, 0.05f);
        Assert.InRange(signal.SmoothedIntensity, 0f, 0.05f);
        Assert.InRange(signal.OutputIntensity, 0f, 0.05f);
        Assert.InRange(signal.SpeedScale, 0f, 1f);
        Assert.Equal(0f, signal.SuspensionAccelerationContribution);
        Assert.Equal(0f, signal.WheelVertForceContribution);
        Assert.Equal(0f, signal.VerticalGContribution);
    }

    [Fact]
    public void HigherSpeedIncreasesSignalWithinCap()
    {
        var evaluator = new RoadTextureEvaluator();

        var slow = evaluator.Evaluate(State(speedKph: 40, surfaceType: 4), Context());
        var fast = evaluator.Evaluate(State(speedKph: 250, surfaceType: 4, frame: 2), Context(now: BaseTime.AddMilliseconds(16)));

        Assert.True(fast.RawIntensity > slow.RawIntensity);
        Assert.InRange(fast.OutputIntensity, 0f, RoadTextureEvaluatorOptions.Default.MaximumIntensity);
    }

    [Fact]
    public void RoadSpeedCurveAndFrequencyContinueChangingPastOneHundredSixtyKph()
    {
        var evaluator = new RoadTextureEvaluator();

        var medium = evaluator.Evaluate(State(speedKph: 160, surfaceType: 0), Context());
        var fast = evaluator.Evaluate(
            State(speedKph: 300, surfaceType: 0, frame: 2),
            Context(now: BaseTime.AddMilliseconds(16)));

        Assert.True(fast.SpeedScale > medium.SpeedScale);
        Assert.True(fast.Bst1FrequencyHz > medium.Bst1FrequencyHz);
        Assert.True(fast.NoiseAmount > medium.NoiseAmount);
    }

    [Fact]
    public void RoadSpeedCurveSupportsLowMediumAndHighF1RangeWithoutUnboundedIntensity()
    {
        var evaluator = new RoadTextureEvaluator();

        var low = evaluator.Evaluate(State(speedKph: 40, surfaceType: 4), Context());
        var medium = evaluator.Evaluate(
            State(speedKph: 160, surfaceType: 4, frame: 2),
            Context(now: BaseTime.AddMilliseconds(16)));
        var high = evaluator.Evaluate(
            State(speedKph: 330, surfaceType: 4, frame: 3),
            Context(now: BaseTime.AddMilliseconds(32)));

        Assert.True(low.SpeedScale > 0f);
        Assert.True(medium.SpeedScale > low.SpeedScale);
        Assert.True(high.SpeedScale > medium.SpeedScale);
        Assert.InRange(high.OutputIntensity, 0f, RoadTextureEvaluatorOptions.Default.MaximumIntensity);
    }

    [Fact]
    public void SurfaceTypeChangesClassAndBaseGain()
    {
        var tarmacEvaluator = new RoadTextureEvaluator();
        var rumbleEvaluator = new RoadTextureEvaluator();

        var tarmac = tarmacEvaluator.Evaluate(State(speedKph: 150, surfaceType: 0), Context());
        var rumble = rumbleEvaluator.Evaluate(State(speedKph: 150, surfaceType: 1), Context());

        Assert.Equal(RoadTextureSurfaceClass.SmoothTrack, tarmac.SurfaceClass);
        Assert.Equal(RoadTextureSurfaceClass.RumbleStrip, rumble.SurfaceClass);
        Assert.True(rumble.RawIntensity > tarmac.RawIntensity);
    }

    [Fact]
    public void SuspensionAccelerationAddsRoughnessOnlyAfterThreshold()
    {
        var calmEvaluator = new RoadTextureEvaluator();
        var roughEvaluator = new RoadTextureEvaluator();

        var calm = calmEvaluator.Evaluate(
            State(speedKph: 130, surfaceType: 0, suspensionAcceleration: Wheels(2f)),
            Context());
        var rough = roughEvaluator.Evaluate(
            State(speedKph: 130, surfaceType: 0, suspensionAcceleration: Wheels(55f)),
            Context());

        Assert.Equal(0f, calm.RoughnessMetric);
        Assert.True(rough.RoughnessMetric > calm.RoughnessMetric);
        Assert.True(rough.SuspensionAccelerationContribution > calm.SuspensionAccelerationContribution);
        Assert.True(rough.RawIntensity > calm.RawIntensity);
    }

    [Fact]
    public void WheelVerticalForceDeltaAddsRoughnessOnlyAfterThreshold()
    {
        var calmEvaluator = new RoadTextureEvaluator();
        var roughEvaluator = new RoadTextureEvaluator();

        var calm = calmEvaluator.Evaluate(
            State(speedKph: 130, surfaceType: 0, wheelVertForce: Wheels(8_000f)),
            Context());
        var rough = roughEvaluator.Evaluate(
            State(
                speedKph: 130,
                surfaceType: 0,
                wheelVertForce: new VehicleWheelData<float>(3_000f, 14_000f, 4_000f, 15_000f)),
            Context());

        Assert.Equal(0f, calm.RoughnessMetric);
        Assert.True(rough.RoughnessMetric > calm.RoughnessMetric);
        Assert.True(rough.WheelVertForceContribution > calm.WheelVertForceContribution);
    }

    [Fact]
    public void SmoothingPreventsAbruptConstantBuzz()
    {
        var evaluator = new RoadTextureEvaluator(RoadTextureEvaluatorOptions.Default with
        {
            AttackSmoothing = 0.25f,
            ReleaseSmoothing = 0.05f
        });

        var first = evaluator.Evaluate(State(speedKph: 250, surfaceType: 1), Context());
        var second = evaluator.Evaluate(State(speedKph: 250, surfaceType: 1, frame: 2), Context(now: BaseTime.AddMilliseconds(16)));
        var released = evaluator.Evaluate(State(speedKph: 0, surfaceType: 1, frame: 3), Context(now: BaseTime.AddMilliseconds(32)));

        Assert.True(first.SmoothedIntensity < first.RawIntensity);
        Assert.True(second.SmoothedIntensity > first.SmoothedIntensity);
        Assert.True(released.SmoothedIntensity > 0f);
        Assert.False(released.IsActive);
    }

    [Fact]
    public void SignalClampsFrequencyAndIntensitySafely()
    {
        var evaluator = new RoadTextureEvaluator();

        var signal = evaluator.Evaluate(
            State(speedKph: 400, surfaceType: 1, suspensionAcceleration: Wheels(999f)),
            Context());

        Assert.InRange(signal.OutputIntensity, 0f, 1f);
        Assert.InRange(signal.Bst1FrequencyHz, 15f, 90f);
        Assert.InRange(signal.PHprFrequencyHz, 1f, 50f);
    }

    [Fact]
    public void GearDuckingReducesOutputWithoutChangingRawSignal()
    {
        var evaluator = new RoadTextureEvaluator();

        var normal = evaluator.Evaluate(State(speedKph: 180, surfaceType: 1), Context());
        var ducked = evaluator.Evaluate(
            State(speedKph: 180, surfaceType: 1, frame: 2),
            Context(now: BaseTime.AddMilliseconds(20), lastGearPulseAtUtc: BaseTime));

        Assert.True(ducked.GearDuckingActive);
        Assert.True(ducked.DuckingGain < 1f);
        Assert.True(ducked.OutputIntensity < ducked.SmoothedIntensity);
        Assert.True(ducked.RawIntensity >= normal.RawIntensity * 0.95f);
    }

    private static RoadTextureEvaluationContext Context(
        DateTimeOffset? now = null,
        bool hapticsRunning = true,
        bool drivingArmed = true,
        bool allowWhenDrivingNotArmed = false,
        bool telemetryStale = false,
        DateTimeOffset? lastGearPulseAtUtc = null)
    {
        return new RoadTextureEvaluationContext(
            now ?? BaseTime,
            hapticsRunning,
            drivingArmed,
            allowWhenDrivingNotArmed,
            telemetryStale,
            lastGearPulseAtUtc);
    }

    private static VehicleState State(
        ushort speedKph,
        byte surfaceType,
        uint frame = 1,
        VehicleWheelData<float>? suspensionAcceleration = null,
        VehicleWheelData<float>? wheelVertForce = null)
    {
        var stamp = new VehicleStateStamp("test", 1, frame / 60f, frame, frame, 0);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(1, frame / 60f, frame, frame, 0, "test"),
            Motion = new VehicleStateSample<VehicleMotionState>(
                new VehicleMotionState(
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    1f,
                    0f,
                    0f,
                    0f),
                stamp),
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(0, 28, 22, 5, 5_000, 10, 1, 0, 0, 0, 0),
                stamp),
            Lap = new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(0, 0, 100f, 100f, 1, 1, 0, 0, 1, 2, 0),
                stamp),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    speedKph,
                    0.4f,
                    0f,
                    0f,
                    0,
                    4,
                    9_500,
                    0,
                    0,
                    0,
                    90,
                    4,
                    Wheels<ushort>(300),
                    Wheels((byte)80),
                    Wheels((byte)80),
                    Wheels(22f),
                    Wheels(surfaceType)),
                stamp),
            CarStatus = new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    0,
                    0,
                    0,
                    55,
                    0,
                    20f,
                    100f,
                    10f,
                    12_000,
                    4_000,
                    8,
                    0,
                    0,
                    16,
                    16,
                    1,
                    0,
                    500_000f,
                    120_000f,
                    3_000_000f,
                    0,
                    0f,
                    0f,
                    0f,
                    0),
                stamp),
            MotionEx = new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    Wheels(0f),
                    Wheels(0f),
                    suspensionAcceleration ?? Wheels(0f),
                    Wheels(speedKph / 3.6f),
                    Wheels(0f),
                    Wheels(0f),
                    Wheels(0f),
                    Wheels(0f),
                    0.2f,
                    0f,
                    0f,
                    speedKph / 3.6f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    wheelVertForce ?? Wheels(8_000f),
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Wheels(0f),
                    Wheels(0f)),
                stamp)
        };
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }
}
