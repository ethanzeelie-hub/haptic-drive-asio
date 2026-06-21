using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Games;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.MockProtocol;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.Tests;

public sealed class PHprContinuousEffectsRuntimeCoordinatorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CoordinatorLivesInActuationAssemblyWithoutWpfOrAppDependencies()
    {
        var assembly = typeof(PHprContinuousEffectsRuntimeCoordinator).Assembly;
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Equal("HapticDrive.Actuation", assembly.GetName().Name);
        Assert.DoesNotContain("HapticDrive.Asio.App", references, StringComparer.Ordinal);
        Assert.DoesNotContain("PresentationFramework", references, StringComparer.Ordinal);
        Assert.DoesNotContain("PresentationCore", references, StringComparer.Ordinal);
        Assert.DoesNotContain("WindowsBase", references, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ConstructingCoordinator_DoesNotWriteCommands()
    {
        await using var harness = new RuntimeHarness();

        Assert.Empty(harness.InnerOutput.CommandHistory);
        Assert.False(harness.Runtime.GetSnapshot().RoadRuntimeStarted);
        Assert.False(harness.Runtime.GetSnapshot().SlipLockRuntimeStarted);
    }

    [Fact]
    public async Task StartingWithAllEffectsDisabled_SendsNoStartCommands()
    {
        await using var harness = new RuntimeHarness(
            roadOptions: PHprRoadVibrationRouterOptions.Disabled,
            slipLockOptions: PHprSlipLockRouterOptions.Disabled);
        harness.CurrentInput = harness.CreateRoadAndSlipInput();

        harness.Runtime.StartSlipLockRuntime();
        harness.Runtime.StartRoadVibrationRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 2);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await Task.Delay(10);

        Assert.Empty(harness.InnerOutput.CommandHistory);
    }

    [Fact]
    public async Task StartingRoadRuntimeWithValidTelemetry_RoutesThroughRoadRouterPath()
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateRoadInput();

        harness.Runtime.StartRoadVibrationRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() =>
            harness.InnerOutput.CommandHistory.Count >= 2
            && harness.RoadRouter.GetSnapshot().LastResult?.WasRouted == true
            && harness.Runtime.GetSnapshot().LastRoadVibrationRoutingResult?.WasRouted == true);

        var commands = harness.InnerOutput.CommandHistory.ToArray();
        Assert.Contains(commands, command => command.Source == PHprCommandSource.RoadTexture && command.TargetModule == PHprModuleId.Brake);
        Assert.Contains(commands, command => command.Source == PHprCommandSource.RoadTexture && command.TargetModule == PHprModuleId.Throttle);
        Assert.True(harness.RoadRouter.GetSnapshot().LastResult?.WasRouted == true);
        Assert.True(harness.Runtime.GetSnapshot().LastRoadVibrationRoutingResult?.WasRouted == true);
    }

    [Fact]
    public async Task StartingSlipLockRuntimeWithValidTelemetry_RoutesThroughSlipLockRouterPath()
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateSlipInput();

        harness.Runtime.StartSlipLockRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() =>
            harness.InnerOutput.CommandHistory.Count >= 1
            && harness.SlipLockRouter.GetSnapshot().LastResult?.WasRouted == true
            && harness.Runtime.GetSnapshot().LastSlipLockRoutingResult?.WasRouted == true);

        var command = Assert.Single(harness.InnerOutput.CommandHistory);
        Assert.Equal(PHprCommandSource.WheelSlip, command.Source);
        Assert.Equal(PHprModuleId.Throttle, command.TargetModule);
        Assert.True(harness.SlipLockRouter.GetSnapshot().LastResult?.WasRouted == true);
        Assert.True(harness.Runtime.GetSnapshot().LastSlipLockRoutingResult?.WasRouted == true);
    }

    [Fact]
    public async Task RoadRuntime_YieldsWhileSlipLockOwnsAModule()
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateRoadAndSlipInput();

        harness.Runtime.StartSlipLockRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() => harness.SlipLockRouter.GetSnapshot().ActiveSlipLockModules != "none");
        harness.InnerOutput.ClearHistory();

        harness.Runtime.StartRoadVibrationRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 2);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() => harness.Runtime.GetSnapshot().RoadHigherPrioritySuppressedCount >= 1);

        Assert.DoesNotContain(harness.InnerOutput.CommandHistory, command => command.Source == PHprCommandSource.RoadTexture && command.DurationMs > 0);
        Assert.Equal(1, harness.Runtime.GetSnapshot().RoadHigherPrioritySuppressedCount);
    }

    [Theory]
    [MemberData(nameof(BlockingRoadContexts))]
    public async Task RoadRuntime_BlockingContextsStopContinuousOutput(
        string scenario,
        Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput> blockedInputFactory)
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateRoadInput();
        harness.Runtime.StartRoadVibrationRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() => harness.RoadRouter.GetSnapshot().ActiveRoadModules != "none");

        harness.CurrentInput = blockedInputFactory(harness);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() => harness.RoadRouter.GetSnapshot().ActiveRoadModules == "none");

        var snapshot = harness.RoadRouter.GetSnapshot();
        Assert.Equal("none", snapshot.ActiveRoadModules);
        Assert.True(snapshot.RoadStopCommandCount >= 1, scenario);
        Assert.Contains(harness.InnerOutput.CommandHistory, command => command.Source == PHprCommandSource.RoadTexture && command.DurationMs == 0);
    }

    [Theory]
    [MemberData(nameof(BlockingSlipLockContexts))]
    public async Task SlipLockRuntime_BlockingContextsStopContinuousOutput(
        string scenario,
        Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput> blockedInputFactory)
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateSlipInput();
        harness.Runtime.StartSlipLockRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() => harness.SlipLockRouter.GetSnapshot().ActiveSlipLockModules != "none");

        harness.CurrentInput = blockedInputFactory(harness);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForAsync(() => harness.SlipLockRouter.GetSnapshot().ActiveSlipLockModules == "none");

        var snapshot = harness.SlipLockRouter.GetSnapshot();
        Assert.Equal("none", snapshot.ActiveSlipLockModules);
        Assert.True(snapshot.StopCommandCount >= 1, scenario);
        Assert.Contains(harness.InnerOutput.CommandHistory, command => command.DurationMs == 0);
    }

    [Fact]
    public async Task CoexistenceConflictSuppressesContinuousStartsThroughSameSafetyPath()
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateRoadInput(
            roadSafetyContext: PHprSafetyContext.DefaultMock with { SoftwareConflictStatus = PHprSoftwareConflictStatus.ActiveConflict });

        harness.Runtime.StartRoadVibrationRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await Task.Delay(10);

        Assert.Empty(harness.InnerOutput.CommandHistory);
        Assert.False(harness.RoadRouter.GetSnapshot().LastResult?.WasRouted ?? false);
    }

    [Fact]
    public async Task StopAndDisposeCancelBackgroundLoopsDeterministically()
    {
        await using var harness = new RuntimeHarness();
        harness.CurrentInput = harness.CreateRoadAndSlipInput();

        harness.Runtime.StartSlipLockRuntime();
        harness.Runtime.StartRoadVibrationRuntime();
        await WaitForAsync(() => harness.Clock.PendingDelayCount >= 2);

        var stopResult = await harness.Runtime.StopAsync(TimeSpan.FromSeconds(1));
        var stoppedSnapshot = harness.Runtime.GetSnapshot();

        Assert.False(stopResult.SlipLockRuntimeTimedOut);
        Assert.False(stopResult.RoadRuntimeTimedOut);
        Assert.False(stoppedSnapshot.SlipLockRuntimeActive);
        Assert.False(stoppedSnapshot.RoadRuntimeActive);

        await harness.Runtime.DisposeAsync();

        var disposedSnapshot = harness.Runtime.GetSnapshot();
        Assert.False(disposedSnapshot.SlipLockRuntimeActive);
        Assert.False(disposedSnapshot.RoadRuntimeActive);
    }

    public static IEnumerable<object[]> BlockingRoadContexts()
    {
        yield return
        [
            "telemetry stale",
            (Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput>)(harness =>
                harness.CreateRoadInput(
                    telemetryTimedOutMuted: true,
                    roadSafetyContext: PHprSafetyContext.DefaultMock with { TelemetryStale = true }))
        ];
        yield return
        [
            "haptics stopped",
            (Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput>)(harness =>
                harness.CreateRoadInput(
                    isRunning: false,
                    roadSafetyContext: PHprSafetyContext.DefaultMock with { HapticsStopped = true }))
        ];
    }

    public static IEnumerable<object[]> BlockingSlipLockContexts()
    {
        yield return
        [
            "telemetry stale",
            (Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput>)(harness =>
                harness.CreateSlipInput(
                    telemetryTimedOutMuted: true,
                    slipLockSafetyContext: PHprSafetyContext.DefaultMock with { TelemetryStale = true }))
        ];
        yield return
        [
            "haptics stopped",
            (Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput>)(harness =>
                harness.CreateSlipInput(
                    isRunning: false,
                    slipLockSafetyContext: PHprSafetyContext.DefaultMock with { HapticsStopped = true }))
        ];
        yield return
        [
            "emergency mute",
            (Func<RuntimeHarness, PHprContinuousEffectsRuntimeInput>)(harness =>
                harness.CreateSlipInput(
                    emergencyMute: true,
                    slipLockSafetyContext: PHprSafetyContext.DefaultMock with { EmergencyMuteActive = true }))
        ];
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.True(predicate());
    }

    public sealed class RuntimeHarness : IAsyncDisposable
    {
        private readonly RoadTextureEvaluator _roadEvaluator = new();

        public RuntimeHarness(
            PHprRoadVibrationRouterOptions? roadOptions = null,
            PHprSlipLockRouterOptions? slipLockOptions = null)
        {
            InnerOutput = new MockPhprOutputDevice();
            Output = new SafetyLimitedPhprOutputDevice(InnerOutput);
            RoadRouter = new PHprRoadVibrationRouter(
                Output,
                roadOptions ?? PHprRoadVibrationRouterOptions.EnabledDefault,
                Output.SetSafetyContext);
            SlipLockRouter = new PHprSlipLockRouter(
                Output,
                slipLockOptions ?? PHprSlipLockRouterOptions.EnabledDefault,
                Output.SetSafetyContext);
            Clock = new FakeRuntimeClock();
            CurrentInput = CreateRoadInput();
            Runtime = new PHprContinuousEffectsRuntimeCoordinator(
                RoadRouter,
                SlipLockRouter,
                () => CurrentInput,
                Clock);
        }

        public MockPhprOutputDevice InnerOutput { get; }

        public SafetyLimitedPhprOutputDevice Output { get; }

        public PHprRoadVibrationRouter RoadRouter { get; }

        public PHprSlipLockRouter SlipLockRouter { get; }

        public FakeRuntimeClock Clock { get; }

        public PHprContinuousEffectsRuntimeCoordinator Runtime { get; }

        public PHprContinuousEffectsRuntimeInput CurrentInput { get; set; }

        public PHprContinuousEffectsRuntimeInput CreateRoadInput(
            bool isRunning = true,
            bool telemetryTimedOutMuted = false,
            bool emergencyMute = false,
            PHprSafetyContext? roadSafetyContext = null,
            PHprSafetyContext? slipLockSafetyContext = null)
        {
            var vehicleState = CreateRoadVehicleState();
            var signal = _roadEvaluator.Evaluate(
                vehicleState,
                new RoadTextureEvaluationContext(
                    Clock.UtcNow,
                    HapticsRunning: isRunning,
                    DrivingArmed: true,
                    AllowWhenDrivingNotArmed: false,
                    TelemetryStale: telemetryTimedOutMuted,
                    LastGearPulseAtUtc: null));
            return CreateInput(
                vehicleState,
                signal,
                isRunning,
                telemetryTimedOutMuted,
                emergencyMute,
                roadSafetyContext ?? CreateContinuousSafetyContext(isRunning, telemetryTimedOutMuted, emergencyMute),
                slipLockSafetyContext ?? CreateContinuousSafetyContext(isRunning, telemetryTimedOutMuted, emergencyMute));
        }

        public PHprContinuousEffectsRuntimeInput CreateSlipInput(
            bool isRunning = true,
            bool telemetryTimedOutMuted = false,
            bool emergencyMute = false,
            PHprSafetyContext? roadSafetyContext = null,
            PHprSafetyContext? slipLockSafetyContext = null)
        {
            var vehicleState = CreateSlipVehicleState();
            return CreateInput(
                vehicleState,
                RoadTextureSignal.Inactive(Clock.UtcNow, "road inactive for slip test"),
                isRunning,
                telemetryTimedOutMuted,
                emergencyMute,
                roadSafetyContext ?? CreateContinuousSafetyContext(isRunning, telemetryTimedOutMuted, emergencyMute),
                slipLockSafetyContext ?? CreateContinuousSafetyContext(isRunning, telemetryTimedOutMuted, emergencyMute));
        }

        public PHprContinuousEffectsRuntimeInput CreateRoadAndSlipInput(
            bool isRunning = true,
            bool telemetryTimedOutMuted = false,
            bool emergencyMute = false,
            PHprSafetyContext? roadSafetyContext = null,
            PHprSafetyContext? slipLockSafetyContext = null)
        {
            var vehicleState = CreateRoadAndSlipVehicleState();
            var signal = _roadEvaluator.Evaluate(
                vehicleState,
                new RoadTextureEvaluationContext(
                    Clock.UtcNow,
                    HapticsRunning: isRunning,
                    DrivingArmed: true,
                    AllowWhenDrivingNotArmed: false,
                    TelemetryStale: telemetryTimedOutMuted,
                    LastGearPulseAtUtc: null));
            return CreateInput(
                vehicleState,
                signal,
                isRunning,
                telemetryTimedOutMuted,
                emergencyMute,
                roadSafetyContext ?? CreateContinuousSafetyContext(isRunning, telemetryTimedOutMuted, emergencyMute),
                slipLockSafetyContext ?? CreateContinuousSafetyContext(isRunning, telemetryTimedOutMuted, emergencyMute));
        }

        public async ValueTask DisposeAsync()
        {
            await Runtime.DisposeAsync();
            await Output.DisposeAsync();
            await InnerOutput.DisposeAsync();
        }

        private PHprContinuousEffectsRuntimeInput CreateInput(
            VehicleState vehicleState,
            RoadTextureSignal signal,
            bool isRunning,
            bool telemetryTimedOutMuted,
            bool emergencyMute,
            PHprSafetyContext roadSafetyContext,
            PHprSafetyContext slipLockSafetyContext)
        {
            var frame = CreateHapticFrame(
                vehicleState,
                isRunning,
                telemetryTimedOutMuted,
                emergencyMute,
                signal,
                Clock.UtcNow);

            return new PHprContinuousEffectsRuntimeInput(
                frame,
                vehicleState,
                ActuationDrivingContextFactory.FromHapticFrame(frame, slipLockSafetyContext.DrivingArmed),
                IsPedalRoutingReady: true,
                roadSafetyContext,
                slipLockSafetyContext);
        }

        private static HapticFrame CreateHapticFrame(
            VehicleState vehicleState,
            bool isRunning,
            bool telemetryTimedOutMuted,
            bool emergencyMute,
            RoadTextureSignal signal,
            DateTimeOffset nowUtc)
        {
            var telemetryFresh = !telemetryTimedOutMuted;
            return new HapticFrame(
                new HapticFrameIdentity(
                    new GameIntegrationId("f1-25"),
                    vehicleState.Frame.Source ?? "test",
                    vehicleState.Frame.SessionUid,
                    vehicleState.Frame.OverallFrameIdentifier,
                    vehicleState.Frame.PlayerCarIndex,
                    nowUtc,
                    0),
                new HapticTelemetrySignals(
                    SpeedMetersPerSecond: vehicleState.Telemetry is null ? null : vehicleState.Telemetry.Value.SpeedKph / 3.6f,
                    Throttle: vehicleState.Telemetry?.Value.Throttle,
                    Brake: vehicleState.Telemetry?.Value.Brake,
                    Steer: vehicleState.Telemetry?.Value.Steer,
                    Gear: vehicleState.Telemetry?.Value.Gear,
                    EngineRpm: vehicleState.Telemetry?.Value.EngineRpm,
                    IdleRpm: vehicleState.CarStatus?.Value.IdleRpm,
                    MaxRpm: vehicleState.CarStatus?.Value.MaxRpm,
                    SurfaceKinds: signal.IsActive
                        ? new HapticWheelSignals<SurfaceKind>(SurfaceKind.RumbleStrip, SurfaceKind.RumbleStrip, SurfaceKind.RumbleStrip, SurfaceKind.RumbleStrip)
                        : new HapticWheelSignals<SurfaceKind>(SurfaceKind.Tarmac, SurfaceKind.Tarmac, SurfaceKind.Tarmac, SurfaceKind.Tarmac),
                    TyreSlip: vehicleState.MotionEx is null
                        ? null
                        : new HapticWheelSignals<float>(
                            vehicleState.MotionEx.Value.WheelSlipRatio.RearLeft,
                            vehicleState.MotionEx.Value.WheelSlipRatio.RearRight,
                            vehicleState.MotionEx.Value.WheelSlipRatio.FrontLeft,
                            vehicleState.MotionEx.Value.WheelSlipRatio.FrontRight),
                    SuspensionVelocity: vehicleState.MotionEx is null
                        ? null
                        : new HapticWheelSignals<float>(
                            vehicleState.MotionEx.Value.SuspensionVelocity.RearLeft,
                            vehicleState.MotionEx.Value.SuspensionVelocity.RearRight,
                            vehicleState.MotionEx.Value.SuspensionVelocity.FrontLeft,
                            vehicleState.MotionEx.Value.SuspensionVelocity.FrontRight),
                    BrakeTemperatureCelsius: vehicleState.Telemetry is null
                        ? null
                        : new HapticWheelSignals<float>(
                            vehicleState.Telemetry.Value.BrakeTemperatureCelsius.RearLeft,
                            vehicleState.Telemetry.Value.BrakeTemperatureCelsius.RearRight,
                            vehicleState.Telemetry.Value.BrakeTemperatureCelsius.FrontLeft,
                            vehicleState.Telemetry.Value.BrakeTemperatureCelsius.FrontRight)),
                new HapticDrivingContext(
                    isRunning && !emergencyMute ? DrivingPhase.Driving : DrivingPhase.Paused,
                    PitState.None,
                    IsPaused: emergencyMute,
                    IsPlayerControlled: true,
                    AllowsDrivingOutput: isRunning && !telemetryTimedOutMuted && !emergencyMute),
                new Dictionary<string, VehicleSignalFreshness>(StringComparer.Ordinal)
                {
                    [HapticFrameSignalNames.Telemetry] = new(telemetryFresh, true, true, true, telemetryFresh, TimeSpan.Zero, 0),
                    [HapticFrameSignalNames.MotionEx] = new(vehicleState.MotionEx is not null, true, true, true, vehicleState.MotionEx is not null, TimeSpan.Zero, 0)
                });
        }
    }

    public sealed class FakeRuntimeClock : IPHprContinuousEffectsRuntimeClock
    {
        private readonly object _gate = new();
        private readonly List<ScheduledDelay> _delays = [];
        private TimeSpan _elapsed;

        public DateTimeOffset UtcNow { get; private set; } = BaseTime;

        public int PendingDelayCount
        {
            get
            {
                lock (_gate)
                {
                    return _delays.Count;
                }
            }
        }

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask(Task.FromCanceled(cancellationToken));
            }

            lock (_gate)
            {
                if (delay <= TimeSpan.Zero)
                {
                    return ValueTask.CompletedTask;
                }

                var scheduled = new ScheduledDelay(_elapsed + delay, cancellationToken);
                _delays.Add(scheduled);
                return new ValueTask(scheduled.Task);
            }
        }

        public void AdvanceBy(TimeSpan delay)
        {
            ScheduledDelay[] ready;
            lock (_gate)
            {
                _elapsed += delay;
                UtcNow = UtcNow.Add(delay);
                ready = _delays.Where(scheduled => scheduled.DueAt <= _elapsed).ToArray();
                foreach (var scheduled in ready)
                {
                    _delays.Remove(scheduled);
                }
            }

            foreach (var scheduled in ready)
            {
                scheduled.Complete();
            }
        }
    }

    private sealed class ScheduledDelay : IDisposable
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _registration;

        public ScheduledDelay(TimeSpan dueAt, CancellationToken cancellationToken)
        {
            DueAt = dueAt;
            _registration = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        }

        public TimeSpan DueAt { get; }

        public Task Task => _completion.Task;

        public void Complete()
        {
            _completion.TrySetResult();
            Dispose();
        }

        public void Dispose()
        {
            _registration.Dispose();
        }
    }

    private static PHprSafetyContext CreateContinuousSafetyContext(
        bool isRunning,
        bool telemetryTimedOutMuted,
        bool emergencyMute)
    {
        return PHprSafetyContext.DefaultMock with
        {
            HapticsStopped = !isRunning,
            TelemetryStale = telemetryTimedOutMuted,
            EmergencyMuteActive = emergencyMute
        };
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
            MotionEx: new VehicleStateSample<VehicleMotionExState>(
                new VehicleMotionExState(
                    SuspensionPosition: Wheels(0f),
                    SuspensionVelocity: Wheels(0f),
                    SuspensionAcceleration: Wheels(0.8f),
                    WheelSpeed: Wheels(30f),
                    WheelSlipRatio: Wheels(0.01f),
                    WheelSlipAngle: Wheels(0.01f),
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
                    WheelVertForce: Wheels(0.6f),
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

    private static VehicleState CreateSlipVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(30f),
            surfaceTypeId: 0,
            includeMotion: false);
    }

    private static VehicleState CreateRoadAndSlipVehicleState(uint frame = 1)
    {
        return CreateVehicleState(
            frame,
            speedKph: 120,
            throttle: 0.8f,
            brake: 0.8f,
            wheelSlipRatio: Wheels(0.42f),
            wheelSlipAngle: Wheels(0.12f),
            wheelSpeed: Wheels(1f),
            surfaceTypeId: 1,
            includeMotion: true);
    }

    private static VehicleState CreateVehicleState(
        uint frame,
        ushort speedKph,
        float throttle,
        float brake,
        VehicleWheelData<float> wheelSlipRatio,
        VehicleWheelData<float> wheelSlipAngle,
        VehicleWheelData<float> wheelSpeed,
        byte surfaceTypeId,
        bool includeMotion)
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
            Motion: includeMotion
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
                        GForceVertical: 1f,
                        Yaw: 0f,
                        Pitch: 0f,
                        Roll: 0f),
                    stamp)
                : null,
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
                    SurfaceTypeIds: Wheels(surfaceTypeId)),
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
                    SuspensionAcceleration: Wheels(0.8f),
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
                    WheelVertForce: Wheels(0.6f),
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
