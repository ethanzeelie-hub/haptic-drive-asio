using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class PHprSlipLockRouterTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DisabledRouterEmitsNoCommands()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.Disabled,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateSlipVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);

        Assert.Equal(PHprSlipLockRoutingStatus.IgnoredDisabled, result.Status);
        Assert.Empty(inner.CommandHistory);
        Assert.Equal(1, router.GetSnapshot().RouteAttemptCount);
    }

    [Fact]
    public async Task SlipRoutesToThrottleByDefault()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateSlipVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var snapshot = router.GetSnapshot();

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Throttle, command.TargetModule);
        Assert.Equal(PHprCommandSource.WheelSlip, command.Source);
        Assert.True(command.DurationMs >= PHprSlipLockEffectSettings.MinimumContinuousDurationMs);
        Assert.Equal("Throttle", snapshot.ActiveSlipLockModules);
        Assert.Contains(inner.FrameHistory, frame => frame.TargetModule == PHprModuleId.Throttle && frame.State == PHprMockProtocolState.Start);
    }

    [Fact]
    public async Task LockRoutesToBrakeByDefault()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateLockVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var snapshot = router.GetSnapshot();

        Assert.True(result.WasRouted, result.Message);
        var command = Assert.Single(inner.CommandHistory);
        Assert.Equal(PHprModuleId.Brake, command.TargetModule);
        Assert.Equal(PHprCommandSource.WheelLock, command.Source);
        Assert.True(command.DurationMs >= PHprSlipLockEffectSettings.MinimumContinuousDurationMs);
        Assert.Equal("Brake", snapshot.ActiveSlipLockModules);
    }

    [Fact]
    public async Task DiagnosticsIntensityMatchesSharedEvaluator()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);
        var state = CreateAllEffectsVehicleState();
        var evaluation = new SlipLockEvaluator().Evaluate(SlipLockEvaluationInput.FromVehicleState(state));

        var result = await router.RouteAsync(state, PHprSafetyContext.DefaultMock, BaseTime);
        var snapshot = router.GetSnapshot();

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(evaluation.WheelSlip.Intensity01, snapshot.WheelSlip.LastIntensity01, precision: 6);
        Assert.Equal(evaluation.WheelLock.Intensity01, snapshot.WheelLock.LastIntensity01, precision: 6);
        Assert.Equal(evaluation.MaximumSlipRatio, (float)snapshot.WheelSlip.LastTelemetry!.MaximumSlipRatio, precision: 6);
        Assert.Equal(evaluation.MaximumSlipAngleRadians, (float)snapshot.WheelSlip.LastTelemetry.MaximumSlipAngle, precision: 6);
    }

    [Fact]
    public async Task PriorityRoutesLockThenSlipPerPedal()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(2, inner.CommandHistory.Count);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelLock && command.TargetModule == PHprModuleId.Brake);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelSlip && command.TargetModule == PHprModuleId.Throttle);
        Assert.Equal(PHprPedalEffectKind.WheelLock, router.GetSnapshot().LastActiveEffect);
    }

    [Fact]
    public async Task ConfigurableSlipAndLockTargetsCanSwapPedals()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault with
            {
                WheelSlip = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelSlip) with
                {
                    TargetModule = PHprGearPulseTarget.Brake
                },
                WheelLock = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelLock) with
                {
                    TargetModule = PHprGearPulseTarget.Throttle
                }
            },
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);

        Assert.True(result.WasRouted, result.Message);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelSlip && command.TargetModule == PHprModuleId.Brake);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelLock && command.TargetModule == PHprModuleId.Throttle);
    }

    [Fact]
    public async Task SlipAndLockCadenceRemainIndependentPerPedal()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault with
            {
                WheelSlip = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelSlip) with
                {
                    TextureCadenceMs = 80
                },
                WheelLock = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelLock) with
                {
                    TextureCadenceMs = 50
                }
            },
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var second = await router.RouteAsync(
            CreateAllEffectsVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(60));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.True(second.WasRouted, second.Message);
        Assert.Equal(3, inner.CommandHistory.Count);
        Assert.Single(second.Commands);
        Assert.Equal(PHprPedalEffectKind.WheelLock, second.Commands[0].Kind);
        Assert.Equal(1, snapshot.WheelSlip.IntervalSuppressedCount);
        Assert.Equal(2, snapshot.WheelLock.RouteCount);
        Assert.Equal(1, snapshot.WheelSlip.RouteCount);
    }

    [Fact]
    public async Task SlipLockRoutesAtBoundedCadenceWithContinuousDurations()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var second = await router.RouteAsync(
            CreateAllEffectsVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(100));
        var routedCommands = inner.CommandHistory
            .Where(command => command.Source is PHprCommandSource.WheelSlip or PHprCommandSource.WheelLock && command.DurationMs > 0)
            .ToArray();
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.True(second.WasRouted, second.Message);
        Assert.Equal(4, routedCommands.Length);
        Assert.All(routedCommands, command => Assert.True(command.DurationMs >= PHprSlipLockEffectSettings.MinimumContinuousDurationMs));
        Assert.Equal("Both", snapshot.ActiveSlipLockModules);
        Assert.Equal("Active", snapshot.RuntimeState);
        Assert.NotNull(snapshot.LastSlipLockUpdateAtUtc);
        Assert.True(snapshot.RouteCount >= 4);
    }

    [Fact]
    public async Task InactiveSlipLockSendsStop()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateSlipVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var inactive = await router.RouteAsync(
            CreateInactiveVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(120));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprSlipLockRoutingStatus.IgnoredNoActiveEffect, inactive.Status);
        Assert.Equal(1, snapshot.StopCommandCount);
        Assert.Equal("none", snapshot.ActiveSlipLockModules);
        Assert.Contains(inner.CommandHistory, command => command.Source == PHprCommandSource.WheelSlip && command.DurationMs == 0);
    }

    [Fact]
    public async Task StaleTelemetryStopsActiveSlipLock()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var stale = await router.RouteAsync(
            CreateAllEffectsVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock with { TelemetryStale = true },
            BaseTime.AddMilliseconds(120));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprSlipLockRoutingStatus.IgnoredNoActiveEffect, stale.Status);
        Assert.Equal(2, snapshot.StaleTelemetrySuppressedCount);
        Assert.Equal(2, snapshot.StopCommandCount);
        Assert.Equal("Idle", snapshot.RuntimeState);
        Assert.Equal("none", snapshot.ActiveSlipLockModules);
        Assert.Contains("stale", snapshot.LastSlipLockStopReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DrivingArmedFalseStopsActiveSlipLock()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateLockVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var blocked = await router.RouteAsync(
            CreateLockVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock with { DrivingArmed = false },
            BaseTime.AddMilliseconds(120));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprSlipLockRoutingStatus.IgnoredNoActiveEffect, blocked.Status);
        Assert.Equal(1, snapshot.StopCommandCount);
        Assert.Equal("none", snapshot.ActiveSlipLockModules);
        Assert.Contains("DrivingArmed", snapshot.LastSlipLockStopReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GearProtectionStopsActiveSlipLockBeforeSuppressingUpdates()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        router.NotifyGearPulseAccepted(BaseTime.AddMilliseconds(100));
        var suppressed = await router.RouteAsync(
            CreateAllEffectsVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(110));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprSlipLockRoutingStatus.IgnoredNoActiveEffect, suppressed.Status);
        Assert.Equal(1, snapshot.GearProtectionSuppressedCount);
        Assert.Equal(2, snapshot.StopCommandCount);
        Assert.Equal("none", snapshot.ActiveSlipLockModules);
        Assert.Contains(inner.CommandHistory, command => command.DurationMs == 0);
    }

    [Fact]
    public async Task HoldTimeoutWatchdogStopsActiveSlipLock()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        await router.StopIfHoldExpiredAsync(BaseTime.AddMilliseconds(500));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(1, snapshot.WatchdogStopCount);
        Assert.Equal(2, snapshot.StopCommandCount);
        Assert.Equal("Idle", snapshot.RuntimeState);
        Assert.Contains("hold timeout", snapshot.LastSlipLockStopReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SafetyContextCanRejectStartCommands()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(
            CreateAllEffectsVehicleState(),
            PHprSafetyContext.DefaultMock with
            {
                BrakeModuleAvailable = false,
                ThrottleModuleAvailable = false
            },
            BaseTime);

        Assert.Equal(PHprSlipLockRoutingStatus.RejectedBySafety, result.Status);
        Assert.Equal(PHprSafetyViolationCode.ModuleUnavailable, output.SafetySnapshot.LastViolation?.Code);
        Assert.True(router.GetSnapshot().SafetyRejectedCount > 0);
    }

    [Fact]
    public async Task MinimumIntervalPreventsSlipLockCommandStorms()
    {
        await using var inner = new MockPhprOutputDevice();
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var second = await router.RouteAsync(
            CreateAllEffectsVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(50));

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprSlipLockRoutingStatus.IgnoredMinimumInterval, second.Status);
        Assert.Equal(2, inner.CommandHistory.Count);
        Assert.True(router.GetSnapshot().IntervalSuppressedCount > 0);
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
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault,
            output.SetSafetyContext);

        var result = await router.RouteAsync(CreateAllEffectsVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var snapshot = router.GetSnapshot();

        Assert.Equal(PHprSlipLockRoutingStatus.Routed, result.Status);
        Assert.Equal(1, snapshot.RouteCount);
        Assert.Equal(1, snapshot.SafetyRejectedCount);
        Assert.Equal(1, snapshot.CommandRateSuppressedCount);
    }

    [Fact]
    public async Task AggressiveCadenceStillRespectsCommandRateLimiter()
    {
        var limits = PHprSafetyLimits.Default with
        {
            MaxCommandsPerSecond = 1,
            AllowRealDeviceWrites = false
        };
        await using var inner = new MockPhprOutputDevice(limits);
        await using var output = new SafetyLimitedPhprOutputDevice(inner);
        var router = new PHprSlipLockRouter(
            output,
            PHprSlipLockRouterOptions.EnabledDefault with
            {
                WheelLock = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelLock) with
                {
                    IsEnabled = false
                },
                WheelSlip = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelSlip) with
                {
                    TextureCadenceMs = PHprSlipLockEffectSettings.MinimumTextureCadenceMs
                }
            },
            output.SetSafetyContext);

        var first = await router.RouteAsync(CreateSlipVehicleState(), PHprSafetyContext.DefaultMock, BaseTime);
        var second = await router.RouteAsync(
            CreateSlipVehicleState(frame: 2),
            PHprSafetyContext.DefaultMock,
            BaseTime.AddMilliseconds(PHprSlipLockEffectSettings.MinimumTextureCadenceMs + 5));
        var snapshot = router.GetSnapshot();

        Assert.True(first.WasRouted, first.Message);
        Assert.Equal(PHprSlipLockRoutingStatus.RejectedBySafety, second.Status);
        Assert.True(snapshot.CommandRateSuppressedCount > 0);
    }

    [Fact]
    public void DiagnosticsExposeContinuousSlipLockFields()
    {
        var snapshot = PHprSlipLockRouterOptions.EnabledDefault.Normalize();

        Assert.Equal(TimeSpan.FromMilliseconds(100), snapshot.MinimumRouteInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(350), snapshot.HoldTimeout);
        Assert.Equal(PHprGearPulseTarget.Throttle, snapshot.WheelSlip.TargetModule);
        Assert.Equal(PHprGearPulseTarget.Brake, snapshot.WheelLock.TargetModule);
        Assert.Equal(70, snapshot.WheelSlip.TextureCadenceMs);
        Assert.Equal(60, snapshot.WheelLock.TextureCadenceMs);
        Assert.True(snapshot.WheelSlip.DurationMs >= PHprSlipLockEffectSettings.MinimumContinuousDurationMs);
        Assert.True(snapshot.WheelLock.DurationMs >= PHprSlipLockEffectSettings.MinimumContinuousDurationMs);
    }

    [Fact]
    public void SlipLockPriorityStaysAboveRoadAndBelowGear()
    {
        var options = PHprSlipLockRouterOptions.EnabledDefault.Normalize();

        Assert.True(options.WheelSlip.Priority > PHprPedalEffectProfile.RoadVibrationDefault.Priority);
        Assert.True(options.WheelLock.Priority > PHprPedalEffectProfile.RoadVibrationDefault.Priority);
        Assert.True(options.WheelLock.Priority > options.WheelSlip.Priority);
        Assert.True(options.WheelSlip.Priority < PHprGearPulseProfile.Default.Priority);
        Assert.True(options.WheelLock.Priority < PHprGearPulseProfile.Default.Priority);
    }

    [Fact]
    public void RouterSurfaceDoesNotExposeAsioAudioOutputPath()
    {
        var constructorParameterNames = typeof(PHprSlipLockRouter)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain("IAudioOutputDevice", constructorParameterNames);
    }

    private static VehicleState CreateSlipVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0f,
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
            wheelSlipRatio: Wheels(0f),
            wheelSlipAngle: Wheels(0f),
            wheelSpeed: Wheels(1f));
    }

    private static VehicleState CreateAllEffectsVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(1f));
    }

    private static VehicleState CreateInactiveVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.02f,
            brake: 0.02f,
            wheelSlipRatio: Wheels(0.01f),
            wheelSlipAngle: Wheels(0.01f),
            wheelSpeed: Wheels(30f));
    }

    private static VehicleState CreateVehicleState(
        uint frame,
        ushort speedKph,
        float throttle,
        float brake,
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
            Motion: null,
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
                    SurfaceTypeIds: Wheels((byte)0)),
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
}
