using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Routing;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class PaddleInputRoutingCoordinatorTests
{
    [Fact]
    public void ConstructingCoordinator_DoesNotRouteOrStartOutput()
    {
        var harness = new CoordinatorHarness();

        Assert.Empty(harness.CallOrder);
        Assert.Empty(harness.NotifiedAcceptedAtUtc);
        Assert.Empty(harness.ManualAsioRequests);
        Assert.Equal(0, harness.DirectRuntime.RouteBenchCallCount);
        Assert.Empty(harness.DirectRuntime.ExceptionCalls);
    }

    [Fact]
    public async Task LeftPaddlePress_FollowsLiveShiftRouteWithoutBenchOrAsio()
    {
        var harness = new CoordinatorHarness(
            benchOptions: DisabledBenchOptions(),
            bst1Settings: DisabledBst1Settings());

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Left, buttonId: 14));

        Assert.False(result.FailedSafely);
        Assert.True(result.ShiftIntentResult?.WasAccepted);
        Assert.Equal(ShiftIntentDirection.Downshift, result.ShiftIntentResult?.ShiftIntentEvent?.Direction);
        Assert.Equal(["notify", "mock-live", "real-live"], harness.CallOrder);
        Assert.Single(harness.NotifiedAcceptedAtUtc);
        Assert.Equal(PaddleSide.Left, harness.LastLiveMockShiftIntent?.PaddleSide);
        Assert.Equal(PaddleSide.Left, harness.LastLiveRealShiftIntent?.PaddleSide);
        Assert.Equal(0, harness.DirectRuntime.RouteBenchCallCount);
        Assert.Empty(harness.ManualAsioRequests);
    }

    [Fact]
    public async Task RightPaddlePress_FollowsLiveShiftRouteWithoutBenchOrAsio()
    {
        var harness = new CoordinatorHarness(
            benchOptions: DisabledBenchOptions(),
            bst1Settings: DisabledBst1Settings());

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Right, buttonId: 13));

        Assert.False(result.FailedSafely);
        Assert.True(result.ShiftIntentResult?.WasAccepted);
        Assert.Equal(ShiftIntentDirection.Upshift, result.ShiftIntentResult?.ShiftIntentEvent?.Direction);
        Assert.Equal(["notify", "mock-live", "real-live"], harness.CallOrder);
        Assert.Single(harness.NotifiedAcceptedAtUtc);
        Assert.Equal(PaddleSide.Right, harness.LastLiveMockShiftIntent?.PaddleSide);
        Assert.Equal(PaddleSide.Right, harness.LastLiveRealShiftIntent?.PaddleSide);
        Assert.Equal(0, harness.DirectRuntime.RouteBenchCallCount);
        Assert.Empty(harness.ManualAsioRequests);
    }

    [Fact]
    public async Task DisabledDirectBenchRoute_DoesNotCallDirectRuntime()
    {
        var harness = new CoordinatorHarness(
            benchOptions: DirectBenchOptions(),
            bst1Settings: DisabledBst1Settings(),
            applyNormalOptionsResult: false);

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Right, buttonId: 13));

        Assert.False(result.FailedSafely);
        Assert.Equal(0, harness.DirectRuntime.RouteBenchCallCount);
        Assert.Equal(1, harness.ApplyNormalOptionsCallCount);
        Assert.Contains("Bench Direct blocked", result.BenchRoutingMessage, StringComparison.Ordinal);
        Assert.Empty(harness.ManualAsioRequests);
    }

    [Fact]
    public async Task DisabledBst1BenchRoute_DoesNotSubmitManualAsioPulse()
    {
        var harness = new CoordinatorHarness(
            benchOptions: MockBenchOptions(),
            bst1Settings: DisabledBst1Settings());

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Left, buttonId: 14));

        Assert.False(result.FailedSafely);
        Assert.Empty(harness.ManualAsioRequests);
        Assert.Equal("BST-1 paddle pulse skipped: disabled.", result.Bst1PaddleGearPulseMessage);
        Assert.Equal(PaddleSide.Left, harness.LastBenchMockShiftIntent?.PaddleSide);
    }

    [Fact]
    public async Task DirectBenchAcceptedPress_UsesDirectRuntimePath()
    {
        var harness = new CoordinatorHarness(
            benchOptions: DirectBenchOptions(),
            bst1Settings: DisabledBst1Settings());

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Right, buttonId: 13));

        Assert.False(result.FailedSafely);
        Assert.Equal(1, harness.ConfigureDirectRuntimeCallCount);
        Assert.Equal(1, harness.ApplyNormalOptionsCallCount);
        Assert.Equal(1, harness.DirectRuntime.RouteBenchCallCount);
        Assert.NotNull(harness.DirectRuntime.LastRouteBenchCall);
        Assert.Equal(PaddleGearBenchTestOutputMode.Direct, harness.DirectRuntime.LastRouteBenchCall?.Options.OutputMode);
        Assert.Equal(PHprGearPulseTarget.Both, harness.DirectRuntime.LastRouteBenchCall?.Options.TargetModule);
        Assert.Equal(PaddleSide.Right, harness.DirectRuntime.LastRouteBenchCall?.BenchResult.PaddleEvent.PaddleSide);
        Assert.Contains("Bench Direct sent via shared direct runtime.", result.BenchRoutingMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RouteException_IsCapturedWithoutThrowing()
    {
        var harness = new CoordinatorHarness(
            benchOptions: DisabledBenchOptions(),
            bst1Settings: DisabledBst1Settings(),
            liveRealRouteException: new InvalidOperationException("boom"));

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Right, buttonId: 13));

        Assert.True(result.FailedSafely);
        Assert.Single(harness.DirectRuntime.ExceptionCalls);
        Assert.Equal("paddle-input-event-exception", harness.DirectRuntime.ExceptionCalls[0].Reason);
        Assert.False(harness.DirectRuntime.ExceptionCalls[0].StopAllIfPulseMayHaveStarted);
        Assert.Empty(harness.CrashLogs);
    }

    [Fact]
    public async Task DirectBenchException_RecoversWithStopAllFlag()
    {
        var harness = new CoordinatorHarness(
            benchOptions: DirectBenchOptions(),
            bst1Settings: DisabledBst1Settings(),
            routeBenchException: new InvalidOperationException("bench failure"));

        var result = await harness.Coordinator.HandleAsync(CreatePaddleEvent(PaddleSide.Left, buttonId: 14));

        Assert.True(result.FailedSafely);
        Assert.Single(harness.DirectRuntime.ExceptionCalls);
        Assert.Equal("paddle-input-event-exception", harness.DirectRuntime.ExceptionCalls[0].Reason);
        Assert.True(harness.DirectRuntime.ExceptionCalls[0].StopAllIfPulseMayHaveStarted);
    }

    [Fact]
    public async Task UiUpdateException_UsesStopAllFalseRecovery()
    {
        var harness = new CoordinatorHarness();
        var exception = new InvalidOperationException("ui update failed");

        await harness.Coordinator.HandleUiUpdateExceptionAsync("paddle-input-route-status-refresh", exception);

        Assert.Single(harness.DirectRuntime.ExceptionCalls);
        Assert.Equal("paddle-input-route-status-refresh", harness.DirectRuntime.ExceptionCalls[0].Reason);
        Assert.False(harness.DirectRuntime.ExceptionCalls[0].StopAllIfPulseMayHaveStarted);
        Assert.Same(exception, harness.DirectRuntime.ExceptionCalls[0].Exception);
    }

    private sealed class CoordinatorHarness
    {
        public CoordinatorHarness(
            PaddleGearBenchTestOptions? benchOptions = null,
            Bst1PaddleGearPulseRouteSettings? bst1Settings = null,
            bool applyNormalOptionsResult = true,
            Exception? liveRealRouteException = null,
            Exception? routeBenchException = null)
        {
            DrivingProvider = new FakeDrivingArmedProvider(DrivingArmedState.Armed("Active driving telemetry is fresh."));
            ShiftProcessor = new ShiftIntentProcessor(DrivingProvider);
            BenchController = new PaddleGearBenchTestController(benchOptions ?? DisabledBenchOptions());
            DirectRuntime = new FakeDirectRuntime
            {
                RouteBenchMessage = "Bench Direct sent via shared direct runtime.",
                RouteBenchException = routeBenchException
            };
            Bst1Settings = bst1Settings ?? DisabledBst1Settings();
            ApplyNormalOptionsResult = applyNormalOptionsResult;
            LiveRealRouteException = liveRealRouteException;

            Coordinator = new PaddleInputRoutingCoordinator(
                ShiftProcessor,
                BenchController,
                DirectRuntime,
                new PaddleInputRoutingCoordinatorDependencies(
                    GetMapping,
                    acceptedAtUtc =>
                    {
                        CallOrder.Add("notify");
                        NotifiedAcceptedAtUtc.Add(acceptedAtUtc);
                    },
                    (shiftIntentEvent, _) =>
                    {
                        CallOrder.Add("mock-live");
                        LastLiveMockShiftIntent = shiftIntentEvent;
                        return ValueTask.FromResult(CreateMockRoutingResult("Live mock route accepted.", shiftIntentEvent));
                    },
                    (shiftIntentEvent, _) =>
                    {
                        CallOrder.Add("real-live");
                        LastLiveRealShiftIntent = shiftIntentEvent;
                        if (LiveRealRouteException is not null)
                        {
                            throw LiveRealRouteException;
                        }

                        return ValueTask.FromResult(CreateRealRoutingResult("Live direct route accepted.", shiftIntentEvent));
                    },
                    (shiftIntentEvent, options, _) =>
                    {
                        CallOrder.Add("bench-mock");
                        LastBenchMockShiftIntent = shiftIntentEvent;
                        LastBenchMockOptions = options;
                        return ValueTask.FromResult(CreateMockRoutingResult("Bench mock route accepted.", shiftIntentEvent));
                    },
                    () => Bst1Settings,
                    (request, _) =>
                    {
                        CallOrder.Add("manual-asio");
                        ManualAsioRequests.Add(request);
                        return ValueTask.FromResult(ManualAsioHardwareTestResult.Success(
                            "Accepted.",
                            ManualAsioSnapshot(
                                selectedOutputChannel: 3,
                                blockedReason: null,
                                source: request.Source,
                                durationMode: request.DurationMode)));
                    },
                    footerMessage =>
                    {
                        CallOrder.Add("apply-normal-options");
                        ApplyNormalOptionsCallCount++;
                        ApplyNormalOptionsFooterMessages.Add(footerMessage);
                        return Task.FromResult(ApplyNormalOptionsResult);
                    },
                    () =>
                    {
                        CallOrder.Add("configure-direct-runtime");
                        ConfigureDirectRuntimeCallCount++;
                    },
                    PaddleSnapshot,
                    moduleId => moduleId == PHprModuleId.Throttle
                        ? PHprRealGearPulseSettings.Default with { IsEnabled = true, DurationMs = 45, Strength01 = 0.2f, FrequencyHz = 50f }
                        : PHprRealGearPulseSettings.Default with { IsEnabled = true, DurationMs = 45, Strength01 = 0.15f, FrequencyHz = 45f },
                    () => PHprSafetyContext.DefaultMock with
                    {
                        IsMockOutput = false,
                        RequiresRealDeviceWrites = true
                    },
                    (reason, exception) => CrashLogs.Add((reason, exception))));
        }

        public PaddleInputRoutingCoordinator Coordinator { get; }

        public FakeDrivingArmedProvider DrivingProvider { get; }

        public ShiftIntentProcessor ShiftProcessor { get; }

        public PaddleGearBenchTestController BenchController { get; }

        public FakeDirectRuntime DirectRuntime { get; }

        public List<string> CallOrder { get; } = [];

        public List<DateTimeOffset> NotifiedAcceptedAtUtc { get; } = [];

        public List<ManualAsioHardwareTestRequest> ManualAsioRequests { get; } = [];

        public List<string> ApplyNormalOptionsFooterMessages { get; } = [];

        public List<(string Reason, Exception? Exception)> CrashLogs { get; } = [];

        public int ApplyNormalOptionsCallCount { get; private set; }

        public int ConfigureDirectRuntimeCallCount { get; private set; }

        public ShiftIntentEvent? LastLiveMockShiftIntent { get; private set; }

        public ShiftIntentEvent? LastLiveRealShiftIntent { get; private set; }

        public ShiftIntentEvent? LastBenchMockShiftIntent { get; private set; }

        public PHprGearPulseRouterOptions? LastBenchMockOptions { get; private set; }

        private Bst1PaddleGearPulseRouteSettings Bst1Settings { get; }

        private bool ApplyNormalOptionsResult { get; }

        private Exception? LiveRealRouteException { get; }
    }

    private sealed class FakeDirectRuntime : IPHprDirectRuntime
    {
        public string RouteBenchMessage { get; init; } = "Bench Direct sent via shared direct runtime.";

        public Exception? RouteBenchException { get; init; }

        public int RouteBenchCallCount { get; private set; }

        public RouteBenchCall? LastRouteBenchCall { get; private set; }

        public List<ExceptionCall> ExceptionCalls { get; } = [];

        public void Configure(PHprDirectRuntimeEnvironment environment)
        {
        }

        public PHprDirectRuntimeSnapshot GetSnapshot()
        {
            throw new NotSupportedException();
        }

        public ValueTask InitializeStartupCleanupAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<PhprDeviceCardPulseResult> SendManualPulseAsync(
            PHprModuleId moduleId,
            PHprRealGearPulseSettings settings,
            PHprSafetyContext safetyContext,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string> RouteBenchAsync(
            PaddleGearBenchTestResult benchResult,
            PaddleGearBenchTestOptions options,
            WheelPaddleInputSnapshot paddleSnapshot,
            Func<PHprModuleId, PHprRealGearPulseSettings> deviceCardSettings,
            PHprSafetyContext safetyContext,
            CancellationToken cancellationToken = default)
        {
            RouteBenchCallCount++;
            LastRouteBenchCall = new RouteBenchCall(
                benchResult,
                options,
                paddleSnapshot,
                safetyContext);

            if (RouteBenchException is not null)
            {
                throw RouteBenchException;
            }

            return ValueTask.FromResult(RouteBenchMessage);
        }

        public ValueTask<PHprDirectStopAllResult> StopAllAsync(string reason, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask EmergencyStopAsync(string reason, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void ClearEmergencyStop()
        {
        }

        public void HandleUnhandledException(string reason, Exception? exception)
        {
        }

        public ValueTask HandlePaddleInputExceptionAsync(
            string reason,
            Exception exception,
            bool stopAllIfPulseMayHaveStarted,
            CancellationToken cancellationToken = default)
        {
            ExceptionCalls.Add(new ExceptionCall(reason, exception, stopAllIfPulseMayHaveStarted));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record RouteBenchCall(
        PaddleGearBenchTestResult BenchResult,
        PaddleGearBenchTestOptions Options,
        WheelPaddleInputSnapshot PaddleSnapshot,
        PHprSafetyContext SafetyContext);

    private sealed record ExceptionCall(
        string Reason,
        Exception Exception,
        bool StopAllIfPulseMayHaveStarted);

    private sealed class FakeDrivingArmedProvider : IDrivingArmedStateProvider
    {
        public FakeDrivingArmedProvider(DrivingArmedState state)
        {
            Current = state;
        }

        public event EventHandler<DrivingArmedState>? DrivingArmedChanged
        {
            add { }
            remove { }
        }

        public DrivingArmedState Current { get; }
    }

    private static PaddleGearBenchTestOptions DisabledBenchOptions()
    {
        return new PaddleGearBenchTestOptions
        {
            IsEnabled = false,
            IsArmed = true,
            OutputMode = PaddleGearBenchTestOutputMode.Direct,
            TargetModule = PHprGearPulseTarget.Both
        };
    }

    private static PaddleGearBenchTestOptions DirectBenchOptions()
    {
        return new PaddleGearBenchTestOptions
        {
            IsEnabled = true,
            IsArmed = true,
            OutputMode = PaddleGearBenchTestOutputMode.Direct,
            TargetModule = PHprGearPulseTarget.Both
        };
    }

    private static PaddleGearBenchTestOptions MockBenchOptions()
    {
        return new PaddleGearBenchTestOptions
        {
            IsEnabled = true,
            IsArmed = true,
            OutputMode = PaddleGearBenchTestOutputMode.Mock,
            TargetModule = PHprGearPulseTarget.Brake
        };
    }

    private static Bst1PaddleGearPulseRouteSettings DisabledBst1Settings()
    {
        return new Bst1PaddleGearPulseRouteSettings(
            IsEnabled: false,
            StrengthPercent: 50,
            OutputTrimPercent: 200,
            FrequencyHz: 50,
            DurationMs: 45,
            DurationMode: "sync");
    }

    private static WheelPaddleMapping GetMapping()
    {
        return new WheelPaddleMapping
        {
            LeftPaddleButtonId = 14,
            RightPaddleButtonId = 13
        };
    }

    private static WheelPaddleInputSnapshot PaddleSnapshot()
    {
        return new WheelPaddleInputSnapshot(
            InputListenerStatus.Listening,
            CreateSelection(),
            GetMapping(),
            InputButtonState.Released,
            InputButtonState.Released,
            LastChangedButtonId: 13,
            LastChangedButtonState: InputButtonState.Pressed,
            LastPaddleEvent: CreatePaddleEvent(PaddleSide.Right, buttonId: 13),
            PaddlePressCount: 1,
            DebounceSuppressedCount: 0,
            LastErrorMessage: null,
            StatusChangedAtUtc: new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero));
    }

    private static WheelPaddleInputEvent CreatePaddleEvent(PaddleSide side, int buttonId)
    {
        return new WheelPaddleInputEvent(
            side,
            CreateSelection(),
            buttonId,
            new InputEventTimestamp(
                new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero).AddMilliseconds(buttonId),
                1000 + buttonId),
            SequenceNumber: buttonId,
            InputButtonState.Pressed);
    }

    private static InputDeviceSelection CreateSelection()
    {
        return new InputDeviceSelection(
            "windowsgamecontroller:gt-neo",
            "Synthetic GT Neo wheel input",
            InputDiscoveryMethod.WindowsGameController,
            NativeDeviceIndex: 0,
            ButtonCount: 32);
    }

    private static PHprGearPulseRoutingResult CreateMockRoutingResult(
        string message,
        ShiftIntentEvent shiftIntentEvent)
    {
        return new PHprGearPulseRoutingResult(
            PHprGearPulseRoutingStatus.Routed,
            message,
            shiftIntentEvent,
            Command: null,
            OutputResult: null,
            SafetySnapshot: null,
            OutputSnapshot: null,
            RoutedAtUtc: shiftIntentEvent.AcceptedAtUtc ?? shiftIntentEvent.TimestampUtc);
    }

    private static PHprDirectGearPulseRoutingResult CreateRealRoutingResult(
        string message,
        ShiftIntentEvent shiftIntentEvent)
    {
        var acceptedAtUtc = shiftIntentEvent.AcceptedAtUtc ?? shiftIntentEvent.TimestampUtc;
        return new PHprDirectGearPulseRoutingResult(
            Routed: true,
            Message: message,
            OutputResults: [],
            CompletedAtUtc: acceptedAtUtc.AddMilliseconds(2),
            ShiftIntentEvent: shiftIntentEvent,
            PaddleEventAtUtc: shiftIntentEvent.TimestampUtc,
            ShiftIntentAcceptedAtUtc: acceptedAtUtc,
            FirstCommandCreatedAtUtc: acceptedAtUtc.AddMilliseconds(1),
            FirstWriteCompletedAtUtc: acceptedAtUtc.AddMilliseconds(2),
            CommandTraces: []);
    }

    private static ManualAsioHardwareTestSnapshot ManualAsioSnapshot(
        int? selectedOutputChannel,
        string? blockedReason,
        string? source,
        string? durationMode)
    {
        return new ManualAsioHardwareTestSnapshot(
            IsActive: false,
            TestMode: "paddle gear bench",
            OutputMode: "ASIO",
            SelectedAsioDriver: "Synthetic ASIO",
            SelectedOutputChannel: selectedOutputChannel,
            AsioRunning: true,
            AsioArmed: true,
            AsioCallbackActive: true,
            HapticsRunning: true,
            EmergencyMute: false,
            NormalMute: false,
            OutputPeakLevel: 0f,
            FramesSubmitted: 0,
            FramesRendered: 0,
            RenderCallbackCount: 0,
            SubmittedFrameCount: 0,
            DroppedFrameCount: 0,
            BackendCallbackCount: 0,
            LastPulseUsedAsio: true,
            LastManualPulseUsedAsio: true,
            LastGearPulseUsedAsio: true,
            LastPulseBlocked: blockedReason is not null,
            LimiterApplied: false,
            PulseGenerationId: 1,
            StaleStopIgnoredCount: 0,
            BlockedReason: blockedReason,
            LastTestSignal: null,
            LastTestDuration: TimeSpan.FromMilliseconds(45),
            LastStrengthPercent: 50f,
            LastOutputTrimPercent: 200f,
            LastEffectivePreLimiterAmplitude: 1f,
            LastEffectivePostLimiterAmplitude: 1f,
            LastFrequencyHz: 50f,
            LastDurationMs: 45,
            LastSource: source,
            LastDurationMode: durationMode,
            ManualPulsePeak: 0f,
            FlightRecorderPath: "synthetic-flight-recorder.jsonl",
            LastError: null);
    }
}
