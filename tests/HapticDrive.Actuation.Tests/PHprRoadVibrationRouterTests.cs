using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class PHprRoadVibrationRouterTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DisabledRouterEmitsNoCommands()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.Disabled,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateRoadVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);

        Assert.Equal(PHprRoadVibrationRoutingStatus.IgnoredDisabled, result.Status);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(1, router.GetSnapshot().RouteAttemptCount);
        Assert.Contains("disabled", router.GetSnapshot().LastIgnoredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoadRoutesToMockBrakeAndThrottleByDefaultWhenEnabled()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateRoadVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(2, inner.CommandHistory.Count);
        Assert.Equal(1, router.GetSnapshot().RouteAttemptCount);
        Assert.Equal(2, router.GetSnapshot().RouteCount);
        Assert.NotNull(router.GetSnapshot().LastCommandRoutedAtUtc);
        Assert.Contains(inner.CommandHistory, command => command.TargetModule == PHprModuleId.Brake && command.Source == PHprCommandSource.RoadTexture);
        Assert.Contains(inner.CommandHistory, command => command.TargetModule == PHprModuleId.Throttle && command.Source == PHprCommandSource.RoadTexture);
        Assert.Contains(inner.FrameHistory, frame => frame.TargetModule == PHprModuleId.Brake && frame.State == PHprMockProtocolState.Start);
        Assert.Contains(inner.FrameHistory, frame => frame.TargetModule == PHprModuleId.Throttle && frame.State == PHprMockProtocolState.Start);
    }

    [Fact]
    public async Task RoadRoutesFromSharedSignal()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.EnabledDefault with
            {
                Brake = PHprRoadVibrationPedalSettings.Default with { DurationMs = 10 },
                Throttle = PHprRoadVibrationPedalSettings.Default with { DurationMs = 10 }
            },
            output.SetSafetyContext);
        var signal = new RoadTextureEvaluator().Evaluate(
            CreateRoadVehicleState(),
            new RoadTextureEvaluationContext(BaseTime, true, true, false, false, null));

        var result = await router.RouteAsync(signal, PHprSafetyContext.DefaultMock, BaseTime);

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(signal, router.GetSnapshot().LastSignal);
        Assert.All(inner.CommandHistory, command => Assert.Equal(PHprCommandSource.RoadTexture, command.Source));
    }

    [Fact]
    public async Task GearDuckingSuppressesRoadCommands()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            output.SetSafetyContext);
        router.NotifyGearPulseAccepted(BaseTime);
        var signal = new RoadTextureEvaluator().Evaluate(
            CreateRoadVehicleState(),
            new RoadTextureEvaluationContext(BaseTime.AddMilliseconds(30), true, true, false, false, BaseTime));

        var result = await router.RouteAsync(signal, PHprSafetyContext.DefaultMock, BaseTime.AddMilliseconds(30));

        Assert.Equal(PHprRoadVibrationRoutingStatus.IgnoredGearDucking, result.Status);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(1, router.GetSnapshot().GearDuckingSuppressedCount);
    }

    [Theory]
    [MemberData(nameof(BlockingContexts))]
    public async Task SafetyContextDropsRoadBeforeCommands(PHprSafetyContext context, PHprSafetyViolationCode expected)
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateRoadVehicleState(), context, BaseTime);

        Assert.Equal(PHprRoadVibrationRoutingStatus.IgnoredNoActiveRoadVibration, result.Status);
        Assert.Contains(
            expected == PHprSafetyViolationCode.TelemetryStale ? "stale" : "active road vibration",
            result.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(output.SafetySnapshot.LastViolation);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(0, router.GetSnapshot().SafetyRejectedCount);
        if (expected == PHprSafetyViolationCode.TelemetryStale)
        {
            Assert.Equal(1, router.GetSnapshot().StaleTelemetrySuppressedCount);
        }
    }

    [Fact]
    public async Task MinimumIntervalPreventsRoadCommandStorms()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateRoadVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var second = await router.RouteAsync(
            CreateRoadVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(50));

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprRoadVibrationRoutingStatus.IgnoredMinimumInterval, second.Status);
        Assert.Equal(2, inner.CommandHistory.Count);
        Assert.Equal(2, router.GetSnapshot().IntervalSuppressedCount);
        Assert.Equal(2, router.GetSnapshot().RouteAttemptCount);
    }

    [Fact]
    public async Task SafetyRejectionCountsCommandRateSuppression()
    {
        var limits = PHprSafetyLimits.Default with
        {
            MaxCommandsPerSecond = 1,
            AllowRealDeviceWrites = false
        };
        await using var inner = new MockPhprOutputDevice(limits);
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprRoadVibrationRouter(
            output,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateRoadVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var snapshot = router.GetSnapshot();

        Assert.Equal(PHprRoadVibrationRoutingStatus.Routed, result.Status);
        Assert.Equal(1, snapshot.RouteCount);
        Assert.Equal(1, snapshot.SafetyRejectedCount);
        Assert.Equal(1, snapshot.CommandRateSuppressedCount);
    }

    [Fact]
    public void RoadPriorityStaysBelowGearSlipAndLock()
    {
        var options = PHprRoadVibrationRouterOptions.EnabledDefault.Normalize();

        Assert.True(options.Priority < PHprGearPulseProfile.Default.Priority);
        Assert.True(options.Priority < PHprPedalEffectProfile.WheelSlipDefault.Priority);
        Assert.True(options.Priority < PHprPedalEffectProfile.WheelLockDefault.Priority);
    }

    [Fact]
    public void RouterSurfaceDoesNotExposeAsioAudioOutputPath()
    {
        var constructorParameterNames = typeof(PHprRoadVibrationRouter)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain("IAudioOutputDevice", constructorParameterNames);
    }

    public static IEnumerable<object[]> BlockingContexts()
    {
        yield return [PHprSafetyContext.DefaultMock with { TelemetryStale = true }, PHprSafetyViolationCode.TelemetryStale];
        yield return [PHprSafetyContext.DefaultMock with { DrivingArmed = false }, PHprSafetyViolationCode.DrivingNotArmed];
    }

    private static VehicleState CreateRoadVehicleState(uint frame = 1)
    {
        var stamp = new VehicleStateStamp(
            "test",
            SessionUid: 7,
            SessionTime: 12.5f,
            FrameIdentifier: frame,
            OverallFrameIdentifier: frame,
            PlayerCarIndex: 0);

        return new VehicleState(
            new VehicleStateFrame(7, 12.5f, frame, frame, 0, "test"),
            Motion: new VehicleStateSample<VehicleMotionState>(
                new VehicleMotionState(
                    WorldPositionX: 0f,
                    WorldPositionY: 0f,
                    WorldPositionZ: 0f,
                    WorldVelocityX: 0f,
                    WorldVelocityY: 0f,
                    WorldVelocityZ: 0f,
                    GForceLateral: 0f,
                    GForceLongitudinal: 0f,
                    GForceVertical: 1f,
                    Yaw: 0f,
                    Pitch: 0f,
                    Roll: 0f),
                stamp),
            Session: new VehicleStateSample<VehicleSessionState>(
                new VehicleSessionState(
                    Weather: 0,
                    TrackTemperatureCelsius: 30,
                    AirTemperatureCelsius: 25,
                    TotalLaps: 10,
                    TrackLengthMeters: 5_000,
                    SessionType: 10,
                    TrackId: 1,
                    GamePaused: 0,
                    SafetyCarStatus: 0,
                    NetworkGame: 1,
                    GameMode: 0),
                stamp),
            Lap: new VehicleStateSample<VehicleLapState>(
                new VehicleLapState(
                    LastLapTimeInMs: 0,
                    CurrentLapTimeInMs: 0,
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
            Participant: null,
            Telemetry: new VehicleStateSample<VehicleTelemetryState>(
                new VehicleTelemetryState(
                    SpeedKph: 120,
                    Throttle: 0.4f,
                    Steer: 0f,
                    Brake: 0f,
                    Clutch: 0,
                    Gear: 4,
                    EngineRpm: 9_500,
                    Drs: 0,
                    RevLightsPercent: 0,
                    RevLightsBitValue: 0,
                    EngineTemperatureCelsius: 90,
                    SuggestedGear: 0,
                    BrakeTemperatureCelsius: Wheels<ushort>(300),
                    TyreSurfaceTemperatureCelsius: Wheels((byte)80),
                    TyreInnerTemperatureCelsius: Wheels((byte)80),
                    TyrePressurePsi: Wheels(22f),
                    SurfaceTypeIds: Wheels((byte)1)),
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
            MotionEx: null,
            LastEvent: null);
    }

    private static VehicleWheelData<T> Wheels<T>(T value)
    {
        return new VehicleWheelData<T>(value, value, value, value);
    }
}
