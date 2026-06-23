using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class ActuationCanonicalInputGuardrailTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActuationRouters_DoNotConsumeVehicleStateOnLivePath()
    {
        var routerParameterTypes = typeof(PHprRoadVibrationRouter)
            .GetMethods()
            .Concat(typeof(PHprSlipLockRouter).GetMethods())
            .Where(method =>
                method.DeclaringType == typeof(PHprRoadVibrationRouter)
                || method.DeclaringType == typeof(PHprSlipLockRouter))
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var runtimeInputPropertyTypes = typeof(PHprContinuousEffectsRuntimeInput)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();
        var roadSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Actuation",
            "PHpr",
            "PHprRoadVibrationRouter.cs"));
        var slipSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Actuation",
            "PHpr",
            "PHprSlipLockRouter.cs"));

        Assert.DoesNotContain(typeof(VehicleState), routerParameterTypes);
        Assert.DoesNotContain(typeof(VehicleState), runtimeInputPropertyTypes);
        Assert.DoesNotContain("VehicleState", roadSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VehicleState", slipSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoadSlipLock_UseCanonicalFrameAndDrivingContext()
    {
        await using var roadInner = new MockPhprOutputDevice();
        await using var roadOutput = new SafetyLimitedPhprOutputDevice(roadInner);
        var roadRouter = new PHprRoadVibrationRouter(
            roadOutput,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            roadOutput.SetSafetyContext);
        var roadFrame = CreateRoadVehicleState().ToCanonicalHapticFrame();
        var roadContext = ActuationDrivingContextFactory.FromHapticFrame(roadFrame, isArmed: true);

        var roadResult = await roadRouter.RouteAsync(roadFrame, roadContext, PHprSafetyContext.DefaultMock, BaseTime);

        Assert.True(roadResult.WasRouted, roadResult.Message);
        Assert.Contains(roadInner.CommandHistory, command => command.Source == PHprCommandSource.RoadTexture);
        Assert.Contains(roadInner.FrameHistory, frame => frame.State == PHprMockProtocolState.Start);

        await using var slipInner = new MockPhprOutputDevice();
        await using var slipOutput = new SafetyLimitedPhprOutputDevice(slipInner);
        var slipRouter = new PHprSlipLockRouter(
            slipOutput,
            PHprSlipLockRouterOptions.EnabledDefault,
            slipOutput.SetSafetyContext);
        var slipFrame = CreateSlipVehicleState().ToCanonicalHapticFrame();
        var slipContext = ActuationDrivingContextFactory.FromHapticFrame(slipFrame, isArmed: true);

        var slipResult = await slipRouter.RouteAsync(slipFrame, slipContext, PHprSafetyContext.DefaultMock, BaseTime);

        Assert.True(slipResult.WasRouted, slipResult.Message);
        Assert.Contains(slipInner.CommandHistory, command => command.Source is PHprCommandSource.WheelSlip or PHprCommandSource.WheelLock);
        Assert.Contains(slipInner.FrameHistory, frame => frame.State == PHprMockProtocolState.Start);
    }

    private static VehicleState CreateRoadVehicleState()
    {
        return CreateVehicleState(
            speedKph: 120,
            throttle: 0.4f,
            brake: 0f,
            surfaces: Wheels((byte)1),
            wheelSlipRatio: Wheels(0f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(33f));
    }

    private static VehicleState CreateSlipVehicleState()
    {
        return CreateVehicleState(
            speedKph: 120,
            throttle: 0.8f,
            brake: 0.8f,
            surfaces: Wheels((byte)0),
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(1f));
    }

    private static VehicleState CreateVehicleState(
        ushort speedKph,
        float throttle,
        float brake,
        VehicleWheelData<byte> surfaces,
        VehicleWheelData<float> wheelSlipRatio,
        VehicleWheelData<float> wheelSlipAngle,
        VehicleWheelData<float> wheelSpeed)
    {
        var stamp = new VehicleStateStamp("test", 7, 12.5f, 1, 1, 0, BaseTime, 1);
        return new VehicleState(
            new VehicleStateFrame(7, 12.5f, 1, 1, 0, "test"),
            Motion: new VehicleStateSample<VehicleMotionState>(
                new VehicleMotionState(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f),
                stamp),
            Session: new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(0, 30, 25, 10, 5_000, 10, 1, 0, 0, 1, 0),
                stamp),
            Lap: new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(0, 0, 100f, 100f, 1, 1, 0, 0, 1, 2, 0),
                stamp),
            Participant: new VehicleStateSample<VehicleParticipantState>(
                new VehicleParticipantState(0, 0, 0, 1, "Player", 1, 0, 0),
                stamp),
            Telemetry: new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: speedKph,
                    Throttle: throttle,
                    Steer: 0f,
                    Brake: brake,
                    Clutch: 0,
                    Gear: 4,
                    EngineRpm: 9_500,
                    Drs: 0,
                    RevLightsPercent: 0,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 90,
                    SuggestedGear: 4,
                    BrakeTemperatureCelsius: Wheels<ushort>(300),
                    TyreSurfaceTemperatureCelsius: Wheels((byte)80),
                    TyreInnerTemperatureCelsius: Wheels((byte)80),
                    TyrePressurePsi: Wheels(22f),
                    SurfaceTypeIds: surfaces),
                stamp),
            CarStatus: new VehicleStateSample<VehicleCarStatusState>(
                new VehicleCarStatusState(
                    TractionControl: 0,
                    AntiLockBrakes: 0,
                    FuelMix: 0,
                    FrontBrakeBias: 55,
                    PitLimiterStatus: 0,
                    FuelInTank: 20f,
                    FuelCapacity: 100f,
                    FuelRemainingLaps: 10f,
                    MaxRpm: 12_000,
                    IdleRpm: 4_000,
                    MaxGears: 8,
                    DrsAllowed: 0,
                    DrsActivationDistance: 0,
                    ActualTyreCompound: 16,
                    VisualTyreCompound: 16,
                    TyresAgeLaps: 1,
                    VehicleFiaFlags: 0,
                    EnginePowerIceWatts: 500_000f,
                    EnginePowerMgukWatts: 120_000f,
                    ErsStoreEnergyJoules: 3_000_000f,
                    ErsDeployMode: 0,
                    ErsHarvestedThisLapMgukJoules: 0f,
                    ErsHarvestedThisLapMguhJoules: 0f,
                    ErsDeployedThisLapJoules: 0f,
                    NetworkPaused: 0),
                stamp),
            Damage: null,
            MotionEx: new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    SuspensionPosition: Wheels(0f),
                    SuspensionVelocity: Wheels(0f),
                    SuspensionAcceleration: Wheels(0f),
                    WheelSpeed: wheelSpeed,
                    WheelSlipRatio: wheelSlipRatio,
                    WheelSlipAngle: wheelSlipAngle,
                    WheelLatForce: Wheels(0f),
                    WheelLongForce: Wheels(0f),
                    HeightOfCogAboveGround: 0.2f,
                    LocalVelocityX: 0f,
                    LocalVelocityY: 0f,
                    LocalVelocityZ: 0f,
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
                stamp),
            LastEvent: null);
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
