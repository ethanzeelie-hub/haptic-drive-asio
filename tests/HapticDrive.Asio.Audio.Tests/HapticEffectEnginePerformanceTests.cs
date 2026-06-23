using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class HapticEffectEnginePerformanceTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void RenderSteadyState_NoLockNoAllocationAfterWarmup()
    {
        foreach (var bufferSize in new[] { 64, 128, 256 })
        {
            var format = new AudioSampleFormat(48_000, 2, bufferSize);
            var engine = new HapticEffectEngine(format, CreateSteadyStateOptions());
            var mixerInputs = new AudioMixerInput[6];
            var durations = new long[10_000];

            engine.Update(CreateActiveRenderFrame(gear: 5, frame: 100));

            for (var i = 0; i < 2_000; i++)
            {
                engine.RenderInto(mixerInputs.AsSpan());
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < durations.Length; i++)
            {
                var started = Stopwatch.GetTimestamp();
                var activeEffectCount = engine.RenderInto(mixerInputs.AsSpan());
                durations[i] = Stopwatch.GetTimestamp() - started;
                Assert.True(activeEffectCount > 0);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.True(allocated <= 1_024, $"Expected <= 1024 allocated bytes after warmup for buffer size {bufferSize}, observed {allocated}.");

            var budget = TimeSpan.FromSeconds((double)bufferSize / format.SampleRate * 0.25d);
            var p99 = MeasureBestP99(
                () =>
                {
                    for (var i = 0; i < durations.Length; i++)
                    {
                        var started = Stopwatch.GetTimestamp();
                        var activeEffectCount = engine.RenderInto(mixerInputs.AsSpan());
                        durations[i] = Stopwatch.GetTimestamp() - started;
                        Assert.True(activeEffectCount > 0);
                    }

                    return durations;
                });
            Assert.True(
                p99 < budget,
                $"Expected p99 render time below {budget.TotalMilliseconds:0.###} ms for buffer size {bufferSize}, observed {p99.TotalMilliseconds:0.###} ms.");
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task ConcurrentSettingsUpdate_DoesNotBlockRender()
    {
        var format = new AudioSampleFormat(48_000, 2, 128);
        var engine = new HapticEffectEngine(format, CreateEngineOptions());
        var mixerInputs = new AudioMixerInput[6];
        var durations = new long[5_000];
        var primaryFrame = CreateActiveRenderFrame(gear: 5, frame: 10);
        var secondaryFrame = CreateActiveRenderFrame(gear: 6, frame: 11);

        engine.Update(primaryFrame);
        for (var i = 0; i < 2_000; i++)
        {
            engine.RenderInto(mixerInputs.AsSpan());
        }

        using var startGate = new ManualResetEventSlim(false);
        var updater = Task.Run(() =>
        {
            startGate.Wait();
            for (var i = 0; i < 750; i++)
            {
                var gain = (i & 1) == 0 ? 0.14f : 0.18f;
                engine.UpdateOptions(CreateEngineOptions() with
                {
                    Engine = CreateEngineOptions().Engine with { Gain = gain }
                });
            }
        });

        startGate.Set();
        for (var i = 0; i < durations.Length; i++)
        {
            engine.Update((i & 1) == 0 ? primaryFrame : secondaryFrame);
            var started = Stopwatch.GetTimestamp();
            engine.RenderInto(mixerInputs.AsSpan());
            durations[i] = Stopwatch.GetTimestamp() - started;
        }

        var completedUpdater = await Task.WhenAny(updater, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(updater, completedUpdater);
        await updater;
        var budget = TimeSpan.FromSeconds((double)format.FrameCount / format.SampleRate * 0.25d);
        var p99 = MeasureBestP99(
            () =>
            {
                for (var i = 0; i < durations.Length; i++)
                {
                    engine.Update((i & 1) == 0 ? primaryFrame : secondaryFrame);
                    var started = Stopwatch.GetTimestamp();
                    engine.RenderInto(mixerInputs.AsSpan());
                    durations[i] = Stopwatch.GetTimestamp() - started;
                }

                return durations;
            });
        Assert.True(p99 < budget, $"Expected concurrent-update render p99 below {budget.TotalMilliseconds:0.###} ms, observed {p99.TotalMilliseconds:0.###} ms.");
        Assert.Equal(0, engine.RenderFailureState.FailureCount);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void RenderException_ClearsOutputAndStoresAtomicFailure()
    {
        var format = new AudioSampleFormat(48_000, 2, 128);
        var engine = new HapticEffectEngine(format, CreateEngineOptions());
        var mixerInputs = new AudioMixerInput[6];

        engine.Update(CreateActiveRenderFrame(gear: 5, frame: 20));
        var activeEffectCount = engine.RenderInto(mixerInputs.AsSpan());
        Assert.True(activeEffectCount > 0);
        Assert.True(engine.GetSnapshot().ActiveEffectCount > 0);

        var failedRenderCount = engine.RenderInto(Span<AudioMixerInput>.Empty);

        Assert.Equal(0, failedRenderCount);
        var failure = engine.RenderFailureState;
        Assert.Equal(1, failure.FailureCount);
        Assert.Equal(HapticRenderFailureCode.RuntimeException, failure.LastFailureCode);

        var snapshot = engine.GetSnapshot();
        Assert.Equal(0, snapshot.ActiveEffectCount);
        Assert.Equal(0f, snapshot.PeakLevel);
    }

    private static HapticEffectEngineOptions CreateEngineOptions()
    {
        return HapticEffectEngineOptions.Default with
        {
            Engine = HapticEffectEngineOptions.Default.Engine with
            {
                IsEnabled = true,
                Gain = 0.14f
            },
            GearShift = HapticEffectEngineOptions.Default.GearShift with
            {
                IsEnabled = true
            },
            Kerb = HapticEffectEngineOptions.Default.Kerb with
            {
                IsEnabled = true
            },
            Impact = HapticEffectEngineOptions.Default.Impact with
            {
                IsEnabled = false
            },
            RoadTexture = HapticEffectEngineOptions.Default.RoadTexture with
            {
                IsEnabled = true
            },
            Slip = HapticEffectEngineOptions.Default.Slip with
            {
                IsEnabled = true,
                WheelSlipEnabled = true
            }
        };
    }

    private static HapticEffectEngineOptions CreateSteadyStateOptions()
    {
        return HapticEffectEngineOptions.Default with
        {
            Engine = HapticEffectEngineOptions.Default.Engine with
            {
                IsEnabled = true,
                Gain = 0.14f
            },
            GearShift = HapticEffectEngineOptions.Default.GearShift with
            {
                IsEnabled = false
            },
            Kerb = HapticEffectEngineOptions.Default.Kerb with
            {
                IsEnabled = true
            },
            Impact = HapticEffectEngineOptions.Default.Impact with
            {
                IsEnabled = false
            },
            RoadTexture = HapticEffectEngineOptions.Default.RoadTexture with
            {
                IsEnabled = false
            },
            Slip = HapticEffectEngineOptions.Default.Slip with
            {
                IsEnabled = false
            }
        };
    }

    private static HapticRenderFrame CreateActiveRenderFrame(sbyte gear, uint frame)
    {
        var vehicleState = CreateVehicleState(gear, frame);
        return LegacyHapticEffectInputFactory.FromVehicleState(vehicleState).RenderFrame;
    }

    private static VehicleState CreateVehicleState(sbyte gear, uint frame)
    {
        var stamp = new VehicleStateStamp("Performance", 42, frame / 10f, frame, frame, 0);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, frame / 10f, frame, frame, 0, "Performance"),
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(
                    Weather: 0,
                    TrackTemperatureCelsius: 28,
                    AirTemperatureCelsius: 22,
                    TotalLaps: 5,
                    TrackLengthMeters: 5_000,
                    SessionType: 10,
                    TrackId: 1,
                    GamePaused: 0,
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
                    PitStatus: 0,
                    Sector: 0,
                    DriverStatus: 1,
                    ResultStatus: 2,
                    CurrentLapInvalid: 0),
                stamp),
            Telemetry = new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: 180,
                    Throttle: 0.85f,
                    Steer: 0f,
                    Brake: 0.2f,
                    Clutch: 0,
                    Gear: gear,
                    EngineRpm: 9_400,
                    Drs: 0,
                    RevLightsPercent: 50,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 95,
                    SuggestedGear: gear,
                    BrakeTemperatureCelsius: Wheels((ushort)350),
                    TyreSurfaceTemperatureCelsius: Wheels((byte)90),
                    TyreInnerTemperatureCelsius: Wheels((byte)90),
                    TyrePressurePsi: Wheels(22.5f),
                    SurfaceTypeIds: Wheels((byte)1)),
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
                stamp),
            MotionEx = new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    SuspensionPosition: Wheels(0f),
                    SuspensionVelocity: Wheels(0f),
                    SuspensionAcceleration: Wheels(0f),
                    WheelSpeed: Wheels(50f),
                    WheelSlipRatio: Wheels(0.3f),
                    WheelSlipAngle: Wheels(0.2f),
                    WheelLatForce: Wheels(0f),
                    WheelLongForce: Wheels(0f),
                    HeightOfCogAboveGround: 0.3f,
                    LocalVelocityX: 0f,
                    LocalVelocityY: 0f,
                    LocalVelocityZ: 50f,
                    AngularVelocityX: 0f,
                    AngularVelocityY: 0f,
                    AngularVelocityZ: 0f,
                    AngularAccelerationX: 0f,
                    AngularAccelerationY: 0f,
                    AngularAccelerationZ: 0f,
                    FrontWheelsAngleRadians: 0f,
                    WheelVertForce: Wheels(8_000f),
                    FrontAeroHeight: 0f,
                    RearAeroHeight: 0f,
                    FrontRollAngle: 0f,
                    RearRollAngle: 0f,
                    ChassisYaw: 0f,
                    ChassisPitch: 0f,
                    WheelCamber: Wheels(0f),
                    WheelCamberGain: Wheels(0f)),
                stamp)
        };
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }

    private static long PercentileTicks(long[] durations, double percentile)
    {
        var copy = (long[])durations.Clone();
        Array.Sort(copy);
        var index = (int)Math.Ceiling((copy.Length * percentile) - 1);
        return copy[Math.Clamp(index, 0, copy.Length - 1)];
    }

    private static TimeSpan MeasureBestP99(Func<long[]> captureDurations, int attempts = 3)
    {
        var best = TimeSpan.MaxValue;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var p99 = ToTimeSpan(PercentileTicks(captureDurations(), 0.99d));
            if (p99 < best)
            {
                best = p99;
            }

            if (attempt < attempts - 1)
            {
                Thread.Sleep(20);
            }
        }

        return best;
    }

    private static TimeSpan ToTimeSpan(long stopwatchTicks)
    {
        return TimeSpan.FromSeconds((double)stopwatchTicks / Stopwatch.Frequency);
    }
}
