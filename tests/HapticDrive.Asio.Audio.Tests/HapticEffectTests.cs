using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;
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

    private static VehicleState State(
        ushort rpm = 9_000,
        float throttle = 0.5f,
        sbyte gear = 3,
        ushort speed = 140,
        ushort idleRpm = 3_000,
        ushort maxRpm = 12_000,
        byte maxGears = 8,
        byte gamePaused = 0,
        byte networkPaused = 0,
        byte driverStatus = 1,
        byte resultStatus = 2,
        byte pitStatus = 0,
        float sessionTime = 1f,
        uint frame = 1)
    {
        var stamp = new VehicleStateStamp("Test", 42, sessionTime, frame, frame, 0);

        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, sessionTime, frame, frame, 0, "Test"),
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
                    Brake: 0f,
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
                    SurfaceTypeIds: Wheels<byte>(0)),
                stamp),
            CarStatus = new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: 0,
                    AntiLockBrakes: 0,
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
