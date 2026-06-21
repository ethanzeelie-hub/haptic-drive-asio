using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticEffectEnginePerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void RenderSteadyStateAllocatesAtMost1024Bytes()
    {
        var format = new AudioSampleFormat(48_000, 2, 128);
        var options = HapticEffectEngineOptions.Default with
        {
            GearShift = GearShiftEffectOptions.Default with { IsEnabled = false },
            Kerb = KerbEffectOptions.Default with { IsEnabled = false },
            Impact = ImpactEffectOptions.Default with { IsEnabled = false },
            RoadTexture = RoadTextureEffectOptions.Default with { IsEnabled = false },
            Slip = SlipEffectOptions.Default with { IsEnabled = false }
        };
        var engine = new HapticEffectEngine(format, options);
        var mixerInputs = new AudioMixerInput[6];
        engine.Update(CreateState(rpm: 9_000, throttle: 0.8f, gear: 5));

        for (var i = 0; i < 64; i++)
        {
            engine.RenderInto(mixerInputs.AsSpan());
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            engine.RenderInto(mixerInputs.AsSpan());
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.True(allocated <= 1_024, $"Expected <= 1024 allocated bytes after warmup, observed {allocated}.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ParameterUpdatePreservesRuntimePhase()
    {
        var format = new AudioSampleFormat(48_000, 1, 128);
        var options = HapticEffectEngineOptions.Default with
        {
            Engine = EngineVibrationEffectOptions.Default with { IsEnabled = false },
            Kerb = KerbEffectOptions.Default with { IsEnabled = false },
            Impact = ImpactEffectOptions.Default with { IsEnabled = false },
            RoadTexture = RoadTextureEffectOptions.Default with { IsEnabled = false },
            Slip = SlipEffectOptions.Default with { IsEnabled = false },
            GearShift = GearShiftEffectOptions.Default with
            {
                IsEnabled = true,
                PulseDuration = TimeSpan.FromMilliseconds(60),
                PulseFrequencyHz = 14f
            }
        };
        var engine = new HapticEffectEngine(format, options);

        engine.Update(CreateState(gear: 2, sessionTime: 1f, frame: 1));
        engine.RenderNextBuffer();
        engine.Update(CreateState(gear: 3, sessionTime: 1.2f, frame: 2));
        var beforeUpdate = engine.RenderNextBuffer();
        var remainingBeforeUpdate = beforeUpdate.Snapshot.GearShift.RemainingPulseFrames;

        engine.UpdateOptions(options with
        {
            GearShift = options.GearShift with { PulseFrequencyHz = 18f }
        });

        var afterUpdate = engine.RenderNextBuffer();
        var remainingAfterUpdate = afterUpdate.Snapshot.GearShift.RemainingPulseFrames;

        Assert.True(remainingBeforeUpdate > 0);
        Assert.True(remainingAfterUpdate > 0);
        Assert.True(
            remainingAfterUpdate < remainingBeforeUpdate,
            $"Expected gear pulse state to continue after parameter update. Before={remainingBeforeUpdate}, After={remainingAfterUpdate}.");
    }

    private static VehicleState CreateState(
        ushort rpm = 9_000,
        float throttle = 0.5f,
        sbyte gear = 3,
        float sessionTime = 1f,
        uint frame = 1)
    {
        var stamp = new VehicleStateStamp("Performance", 42, sessionTime, frame, frame, 0);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, sessionTime, frame, frame, 0, "Performance"),
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(0, 28, 22, 5, 5_000, 10, 1, 0, 0, 0, 0),
                stamp),
            Lap = new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(0, 1_000, 100f, 100f, 1, 1, 0, 0, 1, 2, 0),
                stamp),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: 140,
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
                    BrakeTemperatureCelsius: Wheels((ushort)350),
                    TyreSurfaceTemperatureCelsius: Wheels((byte)90),
                    TyreInnerTemperatureCelsius: Wheels((byte)90),
                    TyrePressurePsi: Wheels(22.5f),
                    SurfaceTypeIds: Wheels((byte)0)),
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
                    MaxRpm: 12_000,
                    IdleRpm: 3_000,
                    MaxGears: 8,
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
                    NetworkPaused: 0),
                stamp)
        };
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }
}
