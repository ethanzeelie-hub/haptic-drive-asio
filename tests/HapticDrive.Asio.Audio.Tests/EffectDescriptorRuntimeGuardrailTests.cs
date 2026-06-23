using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class EffectDescriptorRuntimeGuardrailTests
{
    [Fact]
    public void EffectEngine_ConsumesCanonicalRenderFrame()
    {
        var updateMethod = typeof(HapticEffectEngine).GetMethod(nameof(HapticEffectEngine.Update), [typeof(HapticRenderFrame)]);
        var registry = BuiltInHapticEffectRegistry.Instance;
        var settings = registry.All.ToDictionary(
            descriptor => descriptor.Key,
            descriptor => descriptor.CreateDefaultSettings() with
            {
                Enabled = descriptor.Key is "engine-rpm" or "road-texture"
            },
            StringComparer.OrdinalIgnoreCase);
        var engine = new HapticEffectEngine(new AudioSampleFormat(1_000, 1, 64));
        var mixerInputs = new AudioMixerInput[6];

        engine.UpdateEffectSettings(settings);
        engine.Update(CreateActiveRenderFrame(gear: 5, frame: 1));
        var activeEffectCount = engine.RenderInto(mixerInputs.AsSpan());

        Assert.NotNull(updateMethod);
        Assert.True(activeEffectCount > 0);
    }

    [Fact]
    public void BuiltInDescriptors_CreateRuntimesForEachRegisteredEffect()
    {
        var registry = BuiltInHapticEffectRegistry.Instance;

        foreach (var descriptor in registry.All)
        {
            var runtime = descriptor.CreateRuntime(descriptor.CreateDefaultSettings());

            Assert.NotNull(runtime);
            Assert.Equal(descriptor.Key, runtime.Key);
        }
    }

    [Fact]
    public async Task UnknownEffectKeys_ArePreservedButDoNotRender()
    {
        var path = CreateTempProfilePath();
        var store = new HapticProfileStore();
        var profile = HapticDriveProfile.Default with
        {
            UnknownEffectSettings = new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["custom-future-effect"] = new(
                    "custom-future-effect",
                    Enabled: true,
                    Parameters: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mystery"] = 123d
                    })
            }
        };
        var engine = new HapticEffectEngine(new AudioSampleFormat(1_000, 1, 64));

        var save = await store.SaveAsync(profile, path);
        var load = await store.LoadAsync(path);
        engine.UpdateEffectSettings(load.Profile!.ToEffectSettings());

        Assert.True(save.Succeeded, save.Message);
        Assert.True(load.Succeeded, load.Message);
        Assert.Contains("custom-future-effect", load.Profile.UnknownEffectSettings.Keys);
        Assert.DoesNotContain("custom-future-effect", load.Profile.EffectSettings.Keys);
        Assert.DoesNotContain("custom-future-effect", engine.EffectSettings.Keys);
    }

    private static HapticRenderFrame CreateActiveRenderFrame(sbyte gear, uint frame)
    {
        return LegacyHapticEffectInputFactory.FromVehicleState(CreateVehicleState(gear, frame)).RenderFrame;
    }

    private static VehicleState CreateVehicleState(sbyte gear, uint frame)
    {
        var stamp = new VehicleStateStamp("guardrail", 42, frame / 10f, frame, frame, 0);
        return VehicleState.Empty with
        {
            Frame = new VehicleStateFrame(42, frame / 10f, frame, frame, 0, "guardrail"),
            Session = new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(0, 28, 22, 5, 5_000, 10, 1, 0, 0, 0, 0),
                stamp),
            Lap = new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(0, 1_000, 100f, 100f, 1, 1, 0, 0, 1, 2, 0),
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
                new VehicleCarStatusState(0, 0, 0, 55, 0, 10f, 100f, 5f, 12_000, 3_000, 8, 0, 0, 0, 0, 0, 0, 0f, 0f, 0f, 0, 0f, 0f, 0f, 0),
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

    private static string CreateTempProfilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HapticDrive.Asio.Audio.Tests", "Profiles");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.hdprofile.json");
    }
}
