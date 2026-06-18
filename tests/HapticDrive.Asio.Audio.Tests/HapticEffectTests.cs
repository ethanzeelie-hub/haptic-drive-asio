using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticEffectTests
{
    private static readonly AudioSampleFormat EffectFormat = new(1_000, 1, 200);
    private static readonly AudioSafetyProcessorOptions UnitySafetyOptions = new(
        OutputGain: 1f,
        OutputGainCeiling: 1f,
        LimiterEnabled: true,
        EmergencyMute: false);

    [Fact]
    public void EngineEffect_DisabledOutputsSilence()
    {
        var effect = new EngineVibrationEffect(
            EngineVibrationEffectOptions.Default with { IsEnabled = false });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State());
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void EngineEffect_MissingOrInvalidRpmOutputsSilence()
    {
        var effect = new EngineVibrationEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(VehicleState.Empty);
        effect.Render(buffer);
        AssertSilence(buffer);

        effect.Update(State(rpm: 0));
        effect.Render(buffer);
        AssertSilence(buffer);

        effect.Update(State(rpm: 60_000));
        effect.Render(buffer);
        AssertSilence(buffer);
    }

    [Fact]
    public void EngineEffect_IsDeterministicForFixedVehicleState()
    {
        var first = new EngineVibrationEffect();
        var second = new EngineVibrationEffect();
        var firstBuffer = AudioSampleBuffer.Allocate(EffectFormat);
        var secondBuffer = AudioSampleBuffer.Allocate(EffectFormat);
        var state = State(rpm: 9_000, throttle: 0.65f, gear: 5);

        first.Update(state);
        second.Update(state);
        first.Render(firstBuffer);
        second.Render(secondBuffer);

        AssertSamplesEqual(firstBuffer, secondBuffer);
    }

    [Fact]
    public void EngineEffect_AmplitudeRespondsToThrottle()
    {
        var lowThrottle = RenderEngine(State(rpm: 8_000, throttle: 0.1f));
        var highThrottle = RenderEngine(State(rpm: 8_000, throttle: 1.0f));

        Assert.True(Peak(highThrottle) > Peak(lowThrottle));
    }

    [Fact]
    public void EngineEffect_FrequencyRespondsToRpm()
    {
        var lowEffect = new EngineVibrationEffect();
        var highEffect = new EngineVibrationEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        lowEffect.Update(State(rpm: 4_000, throttle: 0.7f));
        lowEffect.Render(buffer);
        highEffect.Update(State(rpm: 11_000, throttle: 0.7f));
        highEffect.Render(buffer);

        Assert.True(highEffect.Snapshot.CurrentFrequencyHz > lowEffect.Snapshot.CurrentFrequencyHz);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    [InlineData(4f)]
    public void EngineEffect_InvalidThrottleValuesStayFiniteAndBounded(float throttle)
    {
        var buffer = RenderEngine(State(rpm: 8_000, throttle: throttle));

        AssertFiniteAndBounded(buffer, 1f);
    }

    [Fact]
    public void EngineEffect_GatesPausedGarageAndInactiveStates()
    {
        AssertSilence(RenderEngine(State(gamePaused: 1)));
        AssertSilence(RenderEngine(State(networkPaused: 1)));
        AssertSilence(RenderEngine(State(driverStatus: 0)));
        AssertSilence(RenderEngine(State(resultStatus: 1)));
    }

    [Fact]
    public void GearShiftEffect_DoesNotTriggerOnInitialState()
    {
        var effect = new GearShiftEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(gear: 1));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void GearShiftEffect_TriggersOnValidGearChange()
    {
        var effect = new GearShiftEffect(PulseOptions());
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(gear: 1, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(gear: 2, sessionTime: 1.2f, frame: 2));
        var result = effect.Render(buffer);

        Assert.True(result.IsActive);
        Assert.True(Peak(buffer) > 0f);
        Assert.Equal(2u, effect.Snapshot.LastShiftFrameIdentifier);
    }

    [Fact]
    public void GearShiftEffect_DoesNotRetriggerWhenGearIsUnchanged()
    {
        var effect = new GearShiftEffect(PulseOptions(durationMilliseconds: 4));
        var buffer = AudioSampleBuffer.Allocate(new AudioSampleFormat(1_000, 1, 10));

        effect.Update(State(gear: 1, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(gear: 2, sessionTime: 1.2f, frame: 2));
        effect.Render(buffer);
        effect.Update(State(gear: 2, sessionTime: 1.4f, frame: 3));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void GearShiftEffect_HandlesNeutralReverseAndMissingGearSafely()
    {
        var effect = new GearShiftEffect(PulseOptions());
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(VehicleState.Empty);
        effect.Render(buffer);
        AssertSilence(buffer);

        effect.Update(State(gear: 0, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        AssertSilence(buffer);

        effect.Update(State(gear: -1, sessionTime: 1.2f, frame: 2));
        effect.Render(buffer);
        AssertSilence(buffer);
    }

    [Fact]
    public void GearShiftEffect_TransientDecaysOverConfiguredDuration()
    {
        var effect = new GearShiftEffect(PulseOptions(durationMilliseconds: 40));
        var buffer = AudioSampleBuffer.Allocate(new AudioSampleFormat(1_000, 1, 20));

        effect.Update(State(gear: 1, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(gear: 2, sessionTime: 1.2f, frame: 2));
        effect.Render(buffer);
        var firstPeak = Peak(buffer);
        effect.Render(buffer);
        var secondPeak = Peak(buffer);

        Assert.True(firstPeak > 0f);
        Assert.True(secondPeak > 0f);
        Assert.True(secondPeak < firstPeak);
    }

    [Fact]
    public void GearShiftEffect_RapidGearChangesStayBoundedAndDeterministic()
    {
        var first = RenderRapidGearSequence();
        var second = RenderRapidGearSequence();

        Assert.True(Peak(first) <= 0.3f);
        AssertSamplesEqual(first, second);
    }

    [Fact]
    public void KerbEffect_DisabledOutputsSilence()
    {
        var effect = new KerbEffect(KerbEffectOptions.Default with { IsEnabled = false });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(speed: 90, surfaceTypeIds: Wheels<byte>(1)));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void KerbEffect_MissingOrUnknownSurfaceDataOutputsSilence()
    {
        AssertSilence(RenderKerb(VehicleState.Empty));
        AssertSilence(RenderKerb(State(speed: 90, surfaceTypeIds: Wheels<byte>(99))));
    }

    [Fact]
    public void KerbEffect_ActivatesForRumbleStripAndRespondsToSpeed()
    {
        var slow = RenderKerb(State(speed: 25, surfaceTypeIds: Wheels<byte>(1)));
        var fast = RenderKerb(State(speed: 110, surfaceTypeIds: Wheels<byte>(1)));

        Assert.True(Peak(slow) > 0f);
        Assert.True(Peak(fast) > Peak(slow));
    }

    [Fact]
    public void RoadTextureEffect_DisabledOutputsSilence()
    {
        var effect = new RoadTextureEffect(RoadTextureEffectOptions.Default with { IsEnabled = false });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(speed: 90, surfaceTypeIds: Wheels<byte>(4)));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void RoadTextureEffect_IsSilentAtVeryLowSpeed()
    {
        var buffer = RenderRoadTexture(State(speed: 2, surfaceTypeIds: Wheels<byte>(4)));

        AssertSilence(buffer);
    }

    [Fact]
    public void RoadTextureEffect_VariesBySurfaceAndIsDeterministic()
    {
        var firstGravel = RenderRoadTexture(State(speed: 100, surfaceTypeIds: Wheels<byte>(4)));
        var secondGravel = RenderRoadTexture(State(speed: 100, surfaceTypeIds: Wheels<byte>(4)));
        var tarmac = RenderRoadTexture(State(speed: 100, surfaceTypeIds: Wheels<byte>(0)));

        AssertSamplesEqual(firstGravel, secondGravel);
        Assert.True(Peak(firstGravel) > Peak(tarmac));
    }

    [Fact]
    public void RoadTextureEffect_ExposesSharedSignal()
    {
        var effect = new RoadTextureEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(speed: 140, surfaceTypeIds: Wheels<byte>(1)));
        effect.Render(buffer);

        Assert.True(effect.Snapshot.Signal.IsActive);
        Assert.Equal(effect.Snapshot.CurrentFrequencyHz, effect.Snapshot.Signal.Bst1FrequencyHz);
        Assert.True(effect.Snapshot.Signal.OutputIntensity > 0f);
        Assert.True(effect.Snapshot.RmsLevel > 0f);
    }

    [Fact]
    public void RoadTextureEffect_SpeedChangesFrequencyAndGrainBeyondOneHundredSixtyKph()
    {
        var mediumEffect = new RoadTextureEffect();
        var highEffect = new RoadTextureEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        mediumEffect.Update(State(speed: 160, surfaceTypeIds: Wheels<byte>(0)));
        mediumEffect.Render(buffer);
        highEffect.Update(State(speed: 300, surfaceTypeIds: Wheels<byte>(0)));
        highEffect.Render(buffer);

        Assert.True(highEffect.Snapshot.CurrentFrequencyHz > mediumEffect.Snapshot.CurrentFrequencyHz);
        Assert.True(highEffect.Snapshot.Signal.NoiseAmount > mediumEffect.Snapshot.Signal.NoiseAmount);
        Assert.True(highEffect.Snapshot.Signal.SpeedScale > mediumEffect.Snapshot.Signal.SpeedScale);
    }

    [Fact]
    public void RoadTextureEffect_GearPulseDucksSharedSignal()
    {
        var normalEffect = new RoadTextureEffect();
        var duckedEffect = new RoadTextureEffect();
        var normal = AudioSampleBuffer.Allocate(EffectFormat);
        var ducked = AudioSampleBuffer.Allocate(EffectFormat);

        normalEffect.Update(State(speed: 160, surfaceTypeIds: Wheels<byte>(1)));
        normalEffect.Render(normal);
        duckedEffect.NotifyGearPulseAccepted(DateTimeOffset.UtcNow);
        duckedEffect.Update(State(speed: 160, surfaceTypeIds: Wheels<byte>(1)));
        duckedEffect.Render(ducked);

        Assert.True(duckedEffect.Snapshot.Signal.GearDuckingActive);
        Assert.True(Peak(ducked) < Peak(normal));
    }

    [Fact]
    public void RoadTextureEffect_Bst1GainScalesOnlyRenderedAudioOutput()
    {
        var state = State(speed: 160, surfaceTypeIds: Wheels<byte>(0));
        var conservativeEffect = new RoadTextureEffect(RoadTextureEffectOptions.Default with { Gain = 0.25f });
        var tunedEffect = new RoadTextureEffect(RoadTextureEffectOptions.Default with { Gain = 1f });
        var conservative = AudioSampleBuffer.Allocate(EffectFormat);
        var tuned = AudioSampleBuffer.Allocate(EffectFormat);

        conservativeEffect.Update(state);
        tunedEffect.Update(state);
        conservativeEffect.Render(conservative);
        tunedEffect.Render(tuned);

        Assert.Equal(conservativeEffect.Snapshot.Signal.OutputIntensity, tunedEffect.Snapshot.Signal.OutputIntensity, precision: 6);
        Assert.True(Peak(tuned) > Peak(conservative) * 3f);
    }

    [Fact]
    public void RoadTextureEffect_Bst1OutputDisabledKeepsSharedSignalForOtherOutputs()
    {
        var effect = new RoadTextureEffect(RoadTextureEffectOptions.Default with { Bst1OutputEnabled = false });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(speed: 160, surfaceTypeIds: Wheels<byte>(0)));
        var result = effect.Render(buffer);

        Assert.True(effect.Snapshot.IsEnabled);
        Assert.False(effect.Snapshot.Bst1OutputEnabled);
        Assert.True(effect.Snapshot.Signal.IsActive);
        Assert.False(effect.Snapshot.IsActive);
        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void ImpactEffect_DisabledOutputsSilence()
    {
        var effect = new ImpactEffect(ImpactEffectOptions.Default with { IsEnabled = false });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(includeMotion: true, gForceVertical: 1f));
        effect.Update(State(includeMotion: true, gForceVertical: 3f, sessionTime: 1.2f, frame: 2));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void ImpactEffect_DoesNotTriggerOnInitialState()
    {
        var effect = new ImpactEffect(ImpactOptions());
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(includeMotion: true, gForceVertical: 3f, sessionTime: 1f, frame: 1));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void ImpactEffect_TriggersOnVerticalGSpike()
    {
        var effect = new ImpactEffect(ImpactOptions());
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(includeMotion: true, gForceVertical: 1f, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(includeMotion: true, gForceVertical: 3f, sessionTime: 1.2f, frame: 5));
        var result = effect.Render(buffer);

        Assert.True(result.IsActive);
        Assert.True(Peak(buffer) > 0f);
        Assert.Equal(5u, effect.Snapshot.LastImpactFrameIdentifier);
    }

    [Fact]
    public void ImpactEffect_TriggersOnPlayerCollisionEvent()
    {
        var effect = new ImpactEffect(ImpactOptions());
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(sessionTime: 1.2f, frame: 5, eventCode: "COLL", eventInvolvesPlayer: true));
        var result = effect.Render(buffer);

        Assert.True(result.IsActive);
        Assert.True(Peak(buffer) > 0f);
    }

    [Fact]
    public void ImpactEffect_RespectsCooldownForRepeatedSpikes()
    {
        var effect = new ImpactEffect(ImpactOptions(durationMilliseconds: 4, cooldownMilliseconds: 500));
        var buffer = AudioSampleBuffer.Allocate(new AudioSampleFormat(1_000, 1, 10));

        effect.Update(State(includeMotion: true, gForceVertical: 1f, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(includeMotion: true, gForceVertical: 3f, sessionTime: 1.2f, frame: 5));
        effect.Render(buffer);
        Assert.True(Peak(buffer) > 0f);

        effect.Update(State(includeMotion: true, gForceVertical: 1f, sessionTime: 1.25f, frame: 6));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void ImpactEffect_TransientDecaysOverConfiguredDuration()
    {
        var effect = new ImpactEffect(ImpactOptions(durationMilliseconds: 40));
        var buffer = AudioSampleBuffer.Allocate(new AudioSampleFormat(1_000, 1, 20));

        effect.Update(State(includeMotion: true, gForceVertical: 1f, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(includeMotion: true, gForceVertical: 3f, sessionTime: 1.2f, frame: 5));
        effect.Render(buffer);
        var firstPeak = Peak(buffer);
        effect.Render(buffer);
        var secondPeak = Peak(buffer);

        Assert.True(firstPeak > 0f);
        Assert.True(secondPeak > 0f);
        Assert.True(secondPeak < firstPeak);
    }

    [Fact]
    public void SlipEffect_DisabledOutputsSilence()
    {
        var effect = new SlipEffect(SlipEffectOptions.Default with { IsEnabled = false });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(speed: 90, wheelSlipRatio: Wheels(0.4f), wheelSlipAngle: Wheels(0.1f)));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        AssertSilence(buffer);
    }

    [Fact]
    public void SlipEffect_MissingMotionExOutputsSilence()
    {
        var buffer = RenderSlip(State(speed: 90));

        AssertSilence(buffer);
    }

    [Fact]
    public void SlipEffect_ActivatesForMeaningfulSlipRatioOrAngle()
    {
        var ratio = RenderSlip(State(speed: 90, throttle: 0.8f, wheelSlipRatio: Wheels(0.32f), wheelSlipAngle: Wheels(0f)));
        var angle = RenderSlip(State(speed: 90, throttle: 0.8f, wheelSlipRatio: Wheels(0f), wheelSlipAngle: Wheels(0.32f)));

        Assert.True(Peak(ratio) > 0f);
        Assert.True(Peak(angle) > 0f);
    }

    [Fact]
    public void SlipEffect_SnapshotIntensitiesMatchSharedEvaluator()
    {
        var effect = new SlipEffect();
        var state = State(
            speed: 120,
            throttle: 0.8f,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(1f));
        var evaluator = new SlipLockEvaluator(new SlipLockEvaluationOptions(
            MinimumSpeedKph: SlipEffectOptions.Default.MinimumSpeedKph,
            FullIntensitySpeedKph: SlipEffectOptions.Default.FullIntensitySpeedKph,
            SlipRatioThreshold: SlipEffectOptions.Default.SlipRatioThreshold,
            SlipRatioFullScale: SlipEffectOptions.Default.SlipRatioFullScale,
            SlipAngleThresholdRadians: SlipEffectOptions.Default.SlipAngleThresholdRadians,
            SlipAngleFullScaleRadians: SlipEffectOptions.Default.SlipAngleFullScaleRadians,
            TriggerThrottle: SlipEffectOptions.Default.TriggerThrottle,
            TriggerBrake: SlipEffectOptions.Default.TriggerBrake,
            LowPedalInputMultiplier: SlipEffectOptions.Default.LowPedalInputMultiplier,
            AssistedSlipMultiplier: SlipEffectOptions.Default.AssistedSlipMultiplier,
            BrakeLockSlipRatioThreshold: SlipEffectOptions.Default.BrakeLockSlipRatioThreshold,
            BrakeLockWheelSpeedRatioThreshold: SlipEffectOptions.Default.BrakeLockWheelSpeedRatioThreshold,
            MaximumTelemetryFrameLag: SlipEffectOptions.Default.MaximumTelemetryFrameLag));
        var evaluation = evaluator.Evaluate(SlipLockEvaluationInput.FromVehicleState(state, evaluator.Options));

        effect.Update(state);

        Assert.Equal(evaluation.WheelSlip.Intensity01, effect.Snapshot.CurrentSlipIntensity, precision: 6);
        Assert.Equal(evaluation.WheelLock.Intensity01, effect.Snapshot.CurrentLockIntensity, precision: 6);
        Assert.Equal(evaluation.MaximumSlipRatio, effect.Snapshot.CurrentSlipRatio, precision: 6);
        Assert.Equal(evaluation.MaximumSlipAngleRadians, effect.Snapshot.CurrentSlipAngleRadians, precision: 6);
    }

    [Fact]
    public void SlipEffect_UsesSlipTuningWhenWheelSlipDominates()
    {
        var options = SlipEffectOptions.Default with
        {
            WheelSlipFrequencyHz = 57f,
            WheelSlipNoiseAmount = 0.42f,
            WheelLockFrequencyHz = 71f,
            WheelLockNoiseAmount = 0.15f
        };
        var effect = new SlipEffect(options);
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(speed: 90, throttle: 0.8f, wheelSlipRatio: Wheels(0.32f), wheelSlipAngle: Wheels(0.18f)));
        effect.Render(buffer);

        Assert.Equal("Wheel slip", effect.Snapshot.ActiveSource);
        Assert.Equal(57f, effect.Snapshot.CurrentFrequencyHz, precision: 6);
        Assert.Equal(0.42f, effect.Snapshot.CurrentNoiseAmount, precision: 6);
        Assert.True(Peak(buffer) > 0f);
    }

    [Fact]
    public void SlipEffect_SuppressesVeryLowSpeedSlipNoise()
    {
        var buffer = RenderSlip(State(speed: 3, throttle: 1f, wheelSlipRatio: Wheels(1f), wheelSlipAngle: Wheels(1f)));

        AssertSilence(buffer);
    }

    [Fact]
    public void SlipEffect_BrakeLockUsesConservativeSlipPath()
    {
        var effect = new SlipEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(
            speed: 120,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0.5f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(2f)));
        effect.Render(buffer);

        Assert.True(effect.Snapshot.CurrentLockIntensity > 0f);
        Assert.Equal("Wheel lock", effect.Snapshot.ActiveSource);
        Assert.Equal(SlipEffectOptions.Default.WheelLockFrequencyHz, effect.Snapshot.CurrentFrequencyHz, precision: 6);
        Assert.True(Peak(buffer) > 0f);
    }

    [Fact]
    public void SlipEffect_WheelLockCanBeDisabledIndependently()
    {
        var effect = new SlipEffect(SlipEffectOptions.Default with
        {
            WheelLockEnabled = false
        });
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(
            speed: 120,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(2f)));
        var result = effect.Render(buffer);

        Assert.False(result.IsActive);
        Assert.False(effect.Snapshot.IsActive);
        Assert.Equal("below thresholds", effect.Snapshot.ActiveReason);
        AssertSilence(buffer);
    }

    [Fact]
    public void SlipEffect_InvalidValuesStayFiniteAndBounded()
    {
        var buffer = RenderSlip(State(
            speed: 90,
            throttle: 1f,
            wheelSlipRatio: new VehicleWheelData<float>(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -10f),
            wheelSlipAngle: new VehicleWheelData<float>(float.NaN, 0.4f, float.PositiveInfinity, -10f)));

        AssertFiniteAndBounded(buffer, 1f);
    }

    [Fact]
    public async Task EffectEngine_FlowsThroughMixerSafetyAndNullOutputWithoutHardware()
    {
        var configuration = new AudioOutputConfiguration(1_000, 1, 200);
        var format = AudioSampleFormat.FromConfiguration(configuration);
        var engine = new HapticEffectEngine(format);
        var pipeline = new AudioRenderPipeline(format)
        {
            SafetyOptions = UnitySafetyOptions
        };
        var outputBuffer = AudioSampleBuffer.Allocate(format);
        await using var outputDevice = new NullAudioOutputDevice();

        Assert.True((await outputDevice.OpenAsync(configuration)).Succeeded);
        Assert.True((await outputDevice.StartAsync()).Succeeded);
        engine.Update(State(rpm: 9_000, throttle: 0.75f, gear: 4));
        var render = engine.RenderNextBuffer();
        var result = await pipeline.ProcessAndSubmitAsync(render.MixerInputs, outputBuffer, outputDevice);
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.True(render.Snapshot.Engine.IsActive);
        Assert.Equal(1, sink.SubmittedBufferCount);
        Assert.True(sink.LastPeakLevel > 0f);
    }

    [Fact]
    public async Task EffectEngine_Stage13EffectsFlowThroughMixerSafetyAndNullOutputWithoutHardware()
    {
        var configuration = new AudioOutputConfiguration(1_000, 1, 200);
        var format = AudioSampleFormat.FromConfiguration(configuration);
        var engine = new HapticEffectEngine(format);
        var pipeline = new AudioRenderPipeline(format)
        {
            SafetyOptions = UnitySafetyOptions
        };
        var outputBuffer = AudioSampleBuffer.Allocate(format);
        await using var outputDevice = new NullAudioOutputDevice();

        Assert.True((await outputDevice.OpenAsync(configuration)).Succeeded);
        Assert.True((await outputDevice.StartAsync()).Succeeded);
        engine.Update(State(
            rpm: 0,
            speed: 90,
            surfaceTypeIds: Wheels<byte>(1),
            wheelSlipRatio: Wheels(0.3f),
            wheelSlipAngle: Wheels(0.2f),
            wheelSpeed: Wheels(20f)));
        var render = engine.RenderNextBuffer();
        var result = await pipeline.ProcessAndSubmitAsync(render.MixerInputs, outputBuffer, outputDevice);
        var sink = outputDevice.GetSampleSinkSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.True(render.Snapshot.Kerb.IsActive);
        Assert.True(render.Snapshot.RoadTexture.IsActive);
        Assert.True(render.Snapshot.Slip.IsActive);
        Assert.True(render.Snapshot.ActiveEffectCount >= 3);
        Assert.Equal(1, sink.SubmittedBufferCount);
        Assert.True(sink.LastPeakLevel > 0f);
    }

    [Fact]
    public void EffectEngine_MixerInputsFollowDeterministicRegistrationOrder()
    {
        var engine = new HapticEffectEngine(EffectFormat);

        engine.Update(State(
            rpm: 0,
            speed: 90,
            surfaceTypeIds: Wheels<byte>(1),
            wheelSlipRatio: Wheels(0.3f),
            wheelSlipAngle: Wheels(0.2f),
            wheelSpeed: Wheels(20f)));

        var render = engine.RenderNextBuffer();

        Assert.Collection(
            render.MixerInputs,
            input => Assert.Equal("Kerb", input.Name),
            input => Assert.Equal("Road texture", input.Name),
            input => Assert.Equal("Slip", input.Name));
    }

    [Fact]
    public void EffectEngine_EmergencyMuteOutputsSilenceRegardlessOfActiveEffects()
    {
        var engine = new HapticEffectEngine(EffectFormat);
        var pipeline = new AudioRenderPipeline(EffectFormat)
        {
            MixerSettings = new AudioMixerSettings(
                MasterGain: 1f,
                IsMuted: false,
                EmergencyMute: true),
            SafetyOptions = UnitySafetyOptions
        };
        var outputBuffer = AudioSampleBuffer.Allocate(EffectFormat);

        engine.Update(State(rpm: 9_000, throttle: 1f, gear: 3));
        var render = engine.RenderNextBuffer();
        var snapshot = pipeline.Process(render.MixerInputs, outputBuffer);

        Assert.True(render.Snapshot.ActiveEffectCount > 0);
        Assert.True(snapshot.EmergencyMute);
        Assert.Equal(0f, snapshot.OutputPeakLevel);
        AssertSilence(outputBuffer);
    }

    [Fact]
    public void EffectEngine_DeterministicVehicleStateSequenceDrivesEffects()
    {
        var first = RenderVehicleStateSequence();
        var second = RenderVehicleStateSequence();

        AssertSamplesEqual(first[0], second[0]);
        AssertSamplesEqual(first[1], second[1]);
    }

    private static AudioSampleBuffer RenderEngine(VehicleState state)
    {
        var effect = new EngineVibrationEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);
        effect.Update(state);
        effect.Render(buffer);
        return buffer;
    }

    private static AudioSampleBuffer RenderRapidGearSequence()
    {
        var options = PulseOptions(
            gain: 0.3f,
            durationMilliseconds: 80,
            debounceMilliseconds: 100);
        var effect = new GearShiftEffect(options);
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);

        effect.Update(State(gear: 1, sessionTime: 1f, frame: 1));
        effect.Render(buffer);
        effect.Update(State(gear: 2, sessionTime: 1.01f, frame: 2));
        effect.Update(State(gear: 3, sessionTime: 1.02f, frame: 3));
        effect.Render(buffer);
        return buffer;
    }

    private static IReadOnlyList<AudioSampleBuffer> RenderVehicleStateSequence()
    {
        var engine = new HapticEffectEngine(EffectFormat);
        var first = AudioSampleBuffer.Allocate(EffectFormat);
        var second = AudioSampleBuffer.Allocate(EffectFormat);

        engine.Update(State(rpm: 5_000, throttle: 0.4f, gear: 1, sessionTime: 1f, frame: 1));
        CopyMixed(engine.RenderNextBuffer(), first);
        engine.Update(State(rpm: 7_500, throttle: 0.8f, gear: 2, sessionTime: 1.2f, frame: 2));
        CopyMixed(engine.RenderNextBuffer(), second);

        return [first, second];
    }

    private static void CopyMixed(HapticEffectEngineRenderResult render, AudioSampleBuffer destination)
    {
        destination.Clear();
        foreach (var input in render.MixerInputs)
        {
            for (var i = 0; i < destination.SampleCount; i++)
            {
                destination.Samples[i] += input.Buffer.Samples[i];
            }
        }
    }

    private static GearShiftEffectOptions PulseOptions(
        float gain = 0.25f,
        int durationMilliseconds = 80,
        int debounceMilliseconds = 0)
    {
        return GearShiftEffectOptions.Default with
        {
            Gain = gain,
            PulseFrequencyHz = 125f,
            PulseDuration = TimeSpan.FromMilliseconds(durationMilliseconds),
            EngagingDebounceDuration = TimeSpan.FromMilliseconds(debounceMilliseconds)
        };
    }

    private static ImpactEffectOptions ImpactOptions(
        int durationMilliseconds = 80,
        int cooldownMilliseconds = 0)
    {
        return ImpactEffectOptions.Default with
        {
            Gain = 0.25f,
            PulseFrequencyHz = 125f,
            PulseDuration = TimeSpan.FromMilliseconds(durationMilliseconds),
            CooldownDuration = TimeSpan.FromMilliseconds(cooldownMilliseconds),
            MinimumFrameGap = 0
        };
    }

    private static AudioSampleBuffer RenderKerb(VehicleState state)
    {
        var effect = new KerbEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);
        effect.Update(state);
        effect.Render(buffer);
        return buffer;
    }

    private static AudioSampleBuffer RenderRoadTexture(VehicleState state)
    {
        var effect = new RoadTextureEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);
        effect.Update(state);
        effect.Render(buffer);
        return buffer;
    }

    private static AudioSampleBuffer RenderSlip(VehicleState state)
    {
        var effect = new SlipEffect();
        var buffer = AudioSampleBuffer.Allocate(EffectFormat);
        effect.Update(state);
        effect.Render(buffer);
        return buffer;
    }

    private static VehicleState State(
        ushort rpm = 9_000,
        float throttle = 0.5f,
        float brake = 0f,
        sbyte gear = 3,
        ushort speed = 140,
        ushort idleRpm = 3_000,
        ushort maxRpm = 12_000,
        byte maxGears = 8,
        byte tractionControl = 0,
        byte antiLockBrakes = 0,
        byte gamePaused = 0,
        byte networkPaused = 0,
        byte driverStatus = 1,
        byte resultStatus = 2,
        byte pitStatus = 0,
        float sessionTime = 1f,
        uint frame = 1,
        VehicleWheelData<byte>? surfaceTypeIds = null,
        bool includeMotion = false,
        float gForceVertical = 1f,
        VehicleWheelData<float>? suspensionPosition = null,
        VehicleWheelData<float>? suspensionVelocity = null,
        VehicleWheelData<float>? suspensionAcceleration = null,
        VehicleWheelData<float>? wheelSpeed = null,
        VehicleWheelData<float>? wheelSlipRatio = null,
        VehicleWheelData<float>? wheelSlipAngle = null,
        VehicleWheelData<float>? wheelLatForce = null,
        VehicleWheelData<float>? wheelLongForce = null,
        VehicleWheelData<float>? wheelVertForce = null,
        string? eventCode = null,
        bool eventInvolvesPlayer = false)
    {
        var stamp = new VehicleStateStamp("Test", 42, sessionTime, frame, frame, 0);
        var hasMotionEx = suspensionPosition is not null
            || suspensionVelocity is not null
            || suspensionAcceleration is not null
            || wheelSpeed is not null
            || wheelSlipRatio is not null
            || wheelSlipAngle is not null
            || wheelLatForce is not null
            || wheelLongForce is not null
            || wheelVertForce is not null;

        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, sessionTime, frame, frame, 0, "Test"),
            Motion = includeMotion
                ? new VehicleStateSample<VehicleMotionState>(
                    new VehicleMotionState(
                        WorldPositionX: 0f,
                        WorldPositionY: 0f,
                        WorldPositionZ: 0f,
                        WorldVelocityX: 0f,
                        WorldVelocityY: 0f,
                        WorldVelocityZ: 0f,
                        GForceLateral: 0f,
                        GForceLongitudinal: 0f,
                        GForceVertical: gForceVertical,
                        Yaw: 0f,
                        Pitch: 0f,
                        Roll: 0f),
                    stamp)
                : null,
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(
                    Weather: 0,
                    TrackTemperatureCelsius: 28,
                    AirTemperatureCelsius: 22,
                    TotalLaps: 5,
                    TrackLengthMeters: 5_000,
                    SessionType: 10,
                    TrackId: 1,
                    GamePaused: gamePaused,
                    SafetyCarStatus: 0,
                    NetworkGame: 0,
                    GameMode: 0),
                stamp),
            Lap = new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(
                    LastLapTimeInMs: 0,
                    CurrentLapTimeInMs: 1_000,
                    LapDistanceMeters: 100f,
                    TotalDistanceMeters: 100f,
                    CarPosition: 1,
                    CurrentLapNumber: 1,
                    PitStatus: pitStatus,
                    Sector: 0,
                    DriverStatus: driverStatus,
                    ResultStatus: resultStatus,
                    CurrentLapInvalid: 0),
                stamp),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: speed,
                    Throttle: throttle,
                    Steer: 0f,
                    Brake: brake,
                    Clutch: 0,
                    Gear: gear,
                    EngineRpm: rpm,
                    Drs: 0,
                    RevLightsPercent: 50,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 95,
                    SuggestedGear: gear,
                    BrakeTemperatureCelsius: Wheels<ushort>(350),
                    TyreSurfaceTemperatureCelsius: Wheels<byte>(90),
                    TyreInnerTemperatureCelsius: Wheels<byte>(90),
                    TyrePressurePsi: Wheels(22.5f),
                    SurfaceTypeIds: surfaceTypeIds ?? Wheels<byte>(0)),
                stamp),
            CarStatus = new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: tractionControl,
                    AntiLockBrakes: antiLockBrakes,
                    FuelMix: 0,
                    FrontBrakeBias: 55,
                    PitLimiterStatus: 0,
                    FuelInTank: 10f,
                    FuelCapacity: 100f,
                    FuelRemainingLaps: 5f,
                    MaxRpm: maxRpm,
                    IdleRpm: idleRpm,
                    MaxGears: maxGears,
                    DrsAllowed: 0,
                    DrsActivationDistance: 0,
                    ActualTyreCompound: 0,
                    VisualTyreCompound: 0,
                    TyresAgeLaps: 0,
                    VehicleFiaFlags: 0,
                    EnginePowerIceWatts: 0f,
                    EnginePowerMgukWatts: 0f,
                    ErsStoreEnergyJoules: 0f,
                    ErsDeployMode: 0,
                    ErsHarvestedThisLapMgukJoules: 0f,
                    ErsHarvestedThisLapMguhJoules: 0f,
                    ErsDeployedThisLapJoules: 0f,
                    NetworkPaused: networkPaused),
                stamp),
            MotionEx = hasMotionEx
                ? new VehicleStateSample<VehicleMotionExState>(
                    new VehicleMotionExState(
                        SuspensionPosition: suspensionPosition ?? Wheels(0f),
                        SuspensionVelocity: suspensionVelocity ?? Wheels(0f),
                        SuspensionAcceleration: suspensionAcceleration ?? Wheels(0f),
                        WheelSpeed: wheelSpeed ?? Wheels(speed / 3.6f),
                        WheelSlipRatio: wheelSlipRatio ?? Wheels(0f),
                        WheelSlipAngle: wheelSlipAngle ?? Wheels(0f),
                        WheelLatForce: wheelLatForce ?? Wheels(0f),
                        WheelLongForce: wheelLongForce ?? Wheels(0f),
                        HeightOfCogAboveGround: 0.3f,
                        LocalVelocityX: 0f,
                        LocalVelocityY: 0f,
                        LocalVelocityZ: speed / 3.6f,
                        AngularVelocityX: 0f,
                        AngularVelocityY: 0f,
                        AngularVelocityZ: 0f,
                        AngularAccelerationX: 0f,
                        AngularAccelerationY: 0f,
                        AngularAccelerationZ: 0f,
                        FrontWheelsAngleRadians: 0f,
                        WheelVertForce: wheelVertForce ?? Wheels(8_000f),
                        FrontAeroHeight: 0f,
                        RearAeroHeight: 0f,
                        FrontRollAngle: 0f,
                        RearRollAngle: 0f,
                        ChassisYaw: 0f,
                        ChassisPitch: 0f,
                        WheelCamber: Wheels(0f),
                        WheelCamberGain: Wheels(0f)),
                    stamp)
                : null,
            LastEvent = eventCode is null
                ? null
                : new VehicleStateSample<VehicleEventState>(
                    new VehicleEventState(
                        eventCode,
                        eventCode.Select(character => (byte)character).ToArray(),
                        Array.Empty<byte>(),
                        eventInvolvesPlayer ? (byte)0 : (byte)1,
                        eventInvolvesPlayer ? (byte)4 : (byte)5,
                        eventInvolvesPlayer),
                    stamp)
        };
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }

    private static float Peak(AudioSampleBuffer buffer)
    {
        var peak = 0f;
        foreach (var sample in buffer.Samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        return peak;
    }

    private static void AssertSilence(AudioSampleBuffer buffer)
    {
        foreach (var sample in buffer.Samples)
        {
            Assert.Equal(0f, sample);
        }
    }

    private static void AssertFiniteAndBounded(AudioSampleBuffer buffer, float maximumAbsoluteValue)
    {
        foreach (var sample in buffer.Samples)
        {
            Assert.True(float.IsFinite(sample));
            Assert.InRange(sample, -maximumAbsoluteValue, maximumAbsoluteValue);
        }
    }

    private static void AssertSamplesEqual(AudioSampleBuffer expected, AudioSampleBuffer actual)
    {
        Assert.Equal(expected.SampleCount, actual.SampleCount);
        for (var i = 0; i < expected.SampleCount; i++)
        {
            Assert.Equal(expected.Samples[i], actual.Samples[i], precision: 6);
        }
    }
}
