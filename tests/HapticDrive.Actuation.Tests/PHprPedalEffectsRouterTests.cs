using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class PHprPedalEffectsRouterTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DisabledRouter_EmitsNoCommands()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(
            output,
            PHprPedalEffectsRouterOptions.Default with { IsEnabled = false });

        var result = await router.RouteAsync(CreateRoadVehicleState(), nowUtc: BaseTime);

        Assert.Equal(PHprPedalEffectsRoutingStatus.IgnoredDisabled, result.Status);
        Assert.Empty(inner.CommandHistory);
    }

    [Fact]
    public async Task RoadRoutesToBothByDefault()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);

        var result = await router.RouteAsync(CreateRoadVehicleState(), nowUtc: BaseTime);

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Both, command.TargetModule);
        Assert.Equal(PHprCommandSource.RoadTexture, command.Source);
        Assert.Contains(inner.FrameHistory, frame => frame.TargetModule == PHprModuleId.Brake && frame.State == PHprMockProtocolState.Start);
        Assert.Contains(inner.FrameHistory, frame => frame.TargetModule == PHprModuleId.Throttle && frame.State == PHprMockProtocolState.Start);
    }

    [Fact]
    public async Task SlipRoutesToThrottleByDefault()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output, RoadDisabledOptions());

        var result = await router.RouteAsync(CreateSlipVehicleState(), nowUtc: BaseTime);

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Throttle, command.TargetModule);
        Assert.Equal(PHprCommandSource.WheelSlip, command.Source);
    }

    [Fact]
    public async Task LockRoutesToBrakeByDefault()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output, RoadDisabledOptions());

        var result = await router.RouteAsync(CreateLockVehicleState(), nowUtc: BaseTime);

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Brake, command.TargetModule);
        Assert.Equal(PHprCommandSource.WheelLock, command.Source);
    }

    [Fact]
    public async Task CanonicalFrameOverload_UsesActuationDrivingContextWithoutRuntimeSnapshot()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);
        var state = CreateRoadVehicleState();
        var frame = CreateFrame(state);
        var drivingContext = ActuationDrivingContextFactory.FromHapticFrame(frame, isArmed: true);

        var result = await router.RouteAsync(
            frame,
            state,
            drivingContext,
            PHprSafetyContext.DefaultMock,
            BaseTime);

        Assert.True(result.WasRouted, result.Message);
        Assert.Single(inner.CommandHistory);
    }

    [Fact]
    public async Task SlipAndLockDiagnosticsMatchSharedEvaluator()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output, RoadDisabledOptions());
        var state = CreateAllEffectsVehicleState();
        var evaluation = new SlipLockEvaluator().Evaluate(SlipLockEvaluationInput.FromVehicleState(state));

        var result = await router.RouteAsync(state, nowUtc: BaseTime);
        var snapshot = router.GetSnapshot();

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(evaluation.WheelSlip.Intensity01, snapshot.WheelSlip.Intensity01, precision: 6);
        Assert.Equal(evaluation.WheelLock.Intensity01, snapshot.WheelLock.Intensity01, precision: 6);
    }

    [Fact]
    public async Task EffectEnableFlagsSuppressIndividualEffects()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(
            output,
            PHprPedalEffectsRouterOptions.Default with
            {
                RoadVibration = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.RoadVibration) with { IsEnabled = false },
                WheelSlip = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelSlip) with { IsEnabled = false },
                WheelLock = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelLock) with { IsEnabled = false }
            });

        var result = await router.RouteAsync(CreateAllEffectsVehicleState(), nowUtc: BaseTime);

        Assert.Equal(PHprPedalEffectsRoutingStatus.IgnoredNoActiveEffect, result.Status);
        Assert.Empty(inner.CommandHistory);
    }

    [Fact]
    public async Task PriorityPrefersLockThenSlipOverRoadPerModule()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);

        var result = await router.RouteAsync(CreateAllEffectsVehicleState(), nowUtc: BaseTime);

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(2, inner.CommandHistory.Count);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelLock && command.TargetModule == PHprModuleId.Brake);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelSlip && command.TargetModule == PHprModuleId.Throttle);
        Assert.DoesNotContain(inner.CommandHistory, command => command.Source == PHprCommandSource.RoadTexture);
        Assert.Equal(PHprPedalEffectKind.WheelLock, router.GetSnapshot().LastActiveEffect);
    }

    [Theory]
    [MemberData(nameof(BlockingContexts))]
    public async Task SafetyContextBlocksStartCommands(PHprSafetyContext context, PHprSafetyViolationCode expected)
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);

        var result = await router.RouteAsync(CreateRoadVehicleState(), context, BaseTime);

        Assert.Equal(PHprPedalEffectsRoutingStatus.RejectedBySafety, result.Status);
        Assert.Equal(expected, result.SafetySnapshot?.LastViolation?.Code);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(1, router.GetSnapshot().RoadVibration.SafetyRejectedCount);
    }

    [Fact]
    public async Task ProfileNormalizerKeepsScaledStrengthWithinSafetyLimit()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var defaults = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.WheelSlip);
        var router = new PHprPedalEffectsRouter(
            output,
            RoadDisabledOptions() with
            {
                WheelSlip = defaults with
                {
                    Profile = defaults.Profile with { Strength01 = 5d }
                }
            });

        var result = await router.RouteAsync(CreateSlipVehicleState(), nowUtc: BaseTime);

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.InRange(command.Strength01, 0d, PHprSafetyLimits.Default.MaxStrength01);
        Assert.True(command.Strength01 > defaults.Profile.Strength01);
        Assert.False(command.SafetyFlags.HasFlag(PHprSafetyFlags.ClampedStrength));
        Assert.Equal(PHprSafetyDecisionKind.Accepted, result.SafetySnapshot?.LastDecision?.Kind);
    }

    [Fact]
    public async Task MockOutputCommandFrameAndPendingStopCountsUpdate()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);

        await router.RouteAsync(CreateRoadVehicleState(), nowUtc: BaseTime);
        var outputSnapshot = router.GetSnapshot().OutputSnapshot;

        Assert.Equal(1, outputSnapshot.AcceptedCommandCount);
        Assert.Equal(4, outputSnapshot.GeneratedFrameCount);
        Assert.Equal(2, outputSnapshot.PendingScheduledStopCount);
    }

    [Fact]
    public async Task MinimumIntervalPreventsCommandStorms()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);

        var first = await router.RouteAsync(CreateRoadVehicleState(), nowUtc: BaseTime);
        var second = await router.RouteAsync(CreateRoadVehicleState(frame: 2), nowUtc: BaseTime.AddMilliseconds(50));

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprPedalEffectsRoutingStatus.IgnoredMinimumInterval, second.Status);
        Assert.Single(inner.CommandHistory);
        Assert.True(router.GetSnapshot().RoadVibration.IntervalSuppressedCount > 0);
    }

    [Fact]
    public async Task EmergencyStopBlocksUntilCleared()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprPedalEffectsRouter(output);

        await router.EmergencyStopAsync();
        var blocked = await router.RouteAsync(CreateRoadVehicleState(), nowUtc: BaseTime);
        router.ClearEmergencyStop();
        var accepted = await router.RouteAsync(CreateRoadVehicleState(frame: 2), nowUtc: BaseTime.AddMilliseconds(200));

        Assert.Equal(PHprPedalEffectsRoutingStatus.RejectedBySafety, blocked.Status);
        Assert.Equal(PHprSafetyViolationCode.EmergencyStopActive, blocked.SafetySnapshot?.LastViolation?.Code);
        Assert.True(accepted.WasRouted, accepted.Message);
    }

    [Fact]
    public void RouterSurfaceUsesSafetyLimitedMockOutputAndNoRealUsbHidAsioWritePath()
    {
        var constructorParameterNames = typeof(PHprPedalEffectsRouter)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();
        var methodNames = typeof(PHprPedalEffectsRouter)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(PHprPedalEffectsRouter))
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains(nameof(SafetyLimitedPhprOutputDevice), constructorParameterNames);
        Assert.DoesNotContain(nameof(MockPhprOutputDevice), constructorParameterNames);
        Assert.DoesNotContain("IAudioOutputDevice", constructorParameterNames);
        Assert.DoesNotContain(methodNames, name => name.Contains("Usb", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Hid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Write", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Asio", StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<object[]> BlockingContexts()
    {
        yield return [PHprSafetyContext.DefaultMock with { TelemetryStale = true }, PHprSafetyViolationCode.TelemetryStale];
        yield return [PHprSafetyContext.DefaultMock with { EmergencyMuteActive = true }, PHprSafetyViolationCode.EmergencyMuteActive];
        yield return [PHprSafetyContext.DefaultMock with { DrivingArmed = false }, PHprSafetyViolationCode.DrivingNotArmed];
        yield return [PHprSafetyContext.DefaultMock with { HapticsStopped = true }, PHprSafetyViolationCode.HapticsStopped];
    }

    private static PHprPedalEffectsRouterOptions RoadDisabledOptions()
    {
        return PHprPedalEffectsRouterOptions.Default with
        {
            RoadVibration = PHprPedalEffectState.DefaultFor(PHprPedalEffectKind.RoadVibration) with { IsEnabled = false }
        };
    }

    private static VehicleState CreateRoadVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.4f,
            brake: 0f,
            surfaces: Wheels((byte)1),
            wheelSlipRatio: Wheels(0f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(33f));
    }

    private static VehicleState CreateSlipVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0f,
            surfaces: Wheels((byte)0),
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(30f));
    }

    private static VehicleState CreateLockVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0f,
            brake: 0.8f,
            surfaces: Wheels((byte)0),
            wheelSlipRatio: Wheels(0f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(1f));
    }

    private static VehicleState CreateAllEffectsVehicleState()
    {
        return CreateVehicleState(
            frame: 1,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0.8f,
            surfaces: Wheels((byte)1),
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(1f));
    }

    private static VehicleState CreateVehicleState(
        uint frame,
        ushort speedKph,
        float throttle,
        float brake,
        VehicleWheelData<byte> surfaces,
        VehicleWheelData<float> wheelSlipRatio,
        VehicleWheelData<float> wheelSlipAngle,
        VehicleWheelData<float> wheelSpeed)
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
                    SuggestedGear: 0,
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
                    WheelVertForce: Wheels(0f),
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

    private static HapticFrame CreateFrame(VehicleState state)
    {
        return state.ToCanonicalHapticFrame();
    }
}
