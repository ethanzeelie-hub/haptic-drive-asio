using System.IO;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class PHprDirectRuntimeTests
{
    [Fact]
    public async Task StartupCleanupSendsStopReportsOnly()
    {
        using var harness = new RuntimeHarness();
        harness.Runtime.Configure(harness.ReadyEnvironment());

        await harness.Runtime.InitializeStartupCleanupAsync();

        Assert.Equal(2, harness.Writer.Reports.Count);
        Assert.All(harness.Writer.Reports, report => Assert.Equal(PHprHidReportState.Stop, report.State));
        Assert.Equal([0xF1, 0xEC, 0x01, 0x00, 0x0A, 0x00], harness.Writer.Reports[0].Payload.Take(6).ToArray());
        Assert.Equal([0xF1, 0xEC, 0x02, 0x00, 0x0A, 0x00], harness.Writer.Reports[1].Payload.Take(6).ToArray());
        Assert.True(harness.Runtime.GetSnapshot().StartupCleanupSucceeded);
    }

    [Fact]
    public async Task StopAllIsRepeatableStopOnlyAndClearsUncleanMarker()
    {
        using var harness = new RuntimeHarness();
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();
        Assert.True(harness.Store.TryCreate("test marker", out _));

        var first = await harness.Runtime.StopAllAsync("test");
        var second = await harness.Runtime.StopAllAsync("test repeat");

        Assert.True(first.Succeeded, first.Message);
        Assert.True(second.Succeeded, second.Message);
        Assert.False(harness.Store.Exists());
        Assert.Equal(4, harness.Writer.Reports.Count);
        Assert.All(harness.Writer.Reports, report => Assert.True(
            report.State is PHprHidReportState.Stop,
            $"Stop All wrote unexpected {report.State} report."));
    }

    [Fact]
    public async Task BenchCreatesMarkerBeforeStartAndClearsAfterScheduledStop()
    {
        using var harness = new RuntimeHarness();
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();
        harness.Writer.BeforeWrite = report =>
        {
            if (report.State == PHprHidReportState.Start)
            {
                Assert.True(harness.Store.Exists());
            }
        };

        var message = await harness.Runtime.RouteBenchAsync(
            BenchResult(),
            BenchOptions(),
            PaddleSnapshot(),
            module => Card(durationMs: 40),
            DirectSafetyContext());

        Assert.Contains("sent", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(harness.Store.Exists());
        Assert.Single(harness.Writer.Reports);
        Assert.Equal(PHprHidReportState.Start, harness.Writer.Reports[0].State);
        harness.OutputClock.AdvanceBy(TimeSpan.FromMilliseconds(40));
        await WaitForReportsAsync(harness.Writer, 2);
        harness.RuntimeClock.AdvanceBy(TimeSpan.FromMilliseconds(290));
        await WaitForMarkerClearedAsync(harness.Store);
        var snapshot = harness.Runtime.GetSnapshot();

        Assert.False(harness.Store.Exists());
        Assert.Equal(PHprDirectRuntimeState.Idle, snapshot.State);
        Assert.NotNull(snapshot.Latency.PaddleReceivedToStartWriteCompletedMs);
        Assert.Contains(harness.RecorderLines(), line => line.Contains("direct-paddle-bench-start-succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UncleanStartupBlocksBenchUntilStopAllClearsMarker()
    {
        using var harness = new RuntimeHarness(createMarkerBeforeRuntime: true);
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();

        var blocked = await harness.Runtime.RouteBenchAsync(
            BenchResult(),
            BenchOptions(),
            PaddleSnapshot(),
            module => Card(durationMs: 40),
            DirectSafetyContext());

        Assert.Contains("unclean shutdown", blocked, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Writer.Reports.Where(report => report.State == PHprHidReportState.Start));
        Assert.True(harness.Runtime.GetSnapshot().DisabledAfterUncleanShutdown);

        var clear = await harness.Runtime.StopAllAsync("clear after unclean startup");

        Assert.True(clear.Succeeded, clear.Message);
        Assert.False(harness.Store.Exists());
        Assert.DoesNotContain(harness.Writer.Reports, report => report.State == PHprHidReportState.Start);
    }

    [Fact]
    public async Task BenchBlocksWhenSharedPulsePathProofFails()
    {
        using var harness = new RuntimeHarness(sharedProofFactory: () => new PHprDirectSharedPathProof(
            "blue-service",
            "bench-service",
            "writer",
            "writer",
            "encoder",
            "encoder",
            "stop",
            "stop"));
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();

        var blocked = await harness.Runtime.RouteBenchAsync(
            BenchResult(),
            BenchOptions(),
            PaddleSnapshot(),
            module => Card(durationMs: 40),
            DirectSafetyContext());

        Assert.Contains("not routed through proven Devices pulse service", blocked, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Writer.Reports);
    }

    [Fact]
    public async Task FlightRecorderRedactsPrivatePathsAndWritesErrorCategory()
    {
        using var harness = new RuntimeHarness(selector: ReadySelector(@"\\?\hid#vid_3670&pid_0905#private-serial"));
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();

        _ = await harness.Runtime.RouteBenchAsync(
            BenchResult(buttonState: InputButtonState.Released),
            BenchOptions(),
            PaddleSnapshot(),
            module => Card(durationMs: 40),
            DirectSafetyContext());

        var log = string.Join(Environment.NewLine, harness.RecorderLines());
        Assert.DoesNotContain(@"\\?\hid#", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-serial", log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(PHprDirectRuntimeErrorCategory.UserSafetyGate), log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaddleInputExceptionIsRecordedAndAttemptsStopAllWhenDirectPulseMayHaveStarted()
    {
        using var harness = new RuntimeHarness();
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();

        await harness.Runtime.HandlePaddleInputExceptionAsync(
            "paddle-input-event-exception",
            new InvalidOperationException("simulated WPF cross-thread setter failure"),
            stopAllIfPulseMayHaveStarted: true);

        var log = string.Join(Environment.NewLine, harness.RecorderLines());
        Assert.Contains("paddle-input-event-exception", log, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", log, StringComparison.Ordinal);
        Assert.Contains("simulated WPF cross-thread setter failure", log, StringComparison.Ordinal);
        Assert.Equal(2, harness.Writer.Reports.Count);
        Assert.All(harness.Writer.Reports, report => Assert.Equal(PHprHidReportState.Stop, report.State));
        Assert.False(harness.Store.Exists());
    }

    [Fact]
    public async Task DirectBenchRouteCompletesBeforeOffDispatcherUiRefreshRuns()
    {
        using var harness = new RuntimeHarness();
        var dispatcher = new FakeMainWindowUiDispatcher(hasAccess: false);
        var uiRefresh = new FakeUiRefresh();
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();

        var message = await harness.Runtime.RouteBenchAsync(
            BenchResult(),
            BenchOptions(),
            PaddleSnapshot(),
            module => Card(durationMs: 40),
            DirectSafetyContext());
        var posted = MainWindowUiDispatch.BeginInvokeIfRequired(dispatcher, uiRefresh.UpdateRealPhprDirectControlStatus);

        Assert.Contains("sent", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(posted);
        Assert.False(uiRefresh.Updated);
        Assert.Single(harness.Writer.Reports);
        Assert.Equal(PHprHidReportState.Start, harness.Writer.Reports[0].State);

        dispatcher.RunNextOnUiThread();

        Assert.True(uiRefresh.Updated);
    }

    [Fact]
    public async Task BenchRetriggerStartsWhileRuntimeIsActiveAndOldObserverIsIgnored()
    {
        using var harness = new RuntimeHarness();
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();

        var first = await harness.Runtime.RouteBenchAsync(
            BenchResult(sequence: 1, timestampUtc: harness.RuntimeClock.UtcNow),
            BenchOptions(),
            PaddleSnapshot(sequence: 1, timestampUtc: harness.RuntimeClock.UtcNow),
            module => Card(durationMs: 40),
            DirectSafetyContext());
        Assert.Contains("sent", first, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PHprDirectRuntimeState.Active, harness.Runtime.GetSnapshot().State);
        harness.RuntimeClock.AdvanceBy(TimeSpan.FromMilliseconds(10));
        harness.OutputClock.AdvanceBy(TimeSpan.FromMilliseconds(10));
        var second = await harness.Runtime.RouteBenchAsync(
            BenchResult(sequence: 2, timestampUtc: harness.RuntimeClock.UtcNow),
            BenchOptions(),
            PaddleSnapshot(sequence: 2, timestampUtc: harness.RuntimeClock.UtcNow),
            module => Card(durationMs: 40),
            DirectSafetyContext());

        Assert.Contains("sent", second, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, harness.Writer.Reports.Count(report => report.State == PHprHidReportState.Start));
        Assert.Equal(PHprDirectRuntimeState.Active, harness.Runtime.GetSnapshot().State);

        harness.OutputClock.AdvanceBy(TimeSpan.FromMilliseconds(30));
        await WaitForDiagnosticsAsync(harness.Output, diagnostics => diagnostics.StaleStopIgnoredCount == 1);
        harness.RuntimeClock.AdvanceBy(TimeSpan.FromMilliseconds(280));
        await WaitForRecorderLineAsync(harness, "direct-paddle-bench-stale-observer-ignored");

        Assert.True(harness.Store.Exists());
        Assert.Equal(PHprDirectRuntimeState.Active, harness.Runtime.GetSnapshot().State);
        Assert.DoesNotContain(harness.Writer.Reports, report => report.State == PHprHidReportState.EmergencyStop);

        harness.OutputClock.AdvanceBy(TimeSpan.FromMilliseconds(10));
        await WaitForReportsAsync(harness.Writer, 3);
        harness.RuntimeClock.AdvanceBy(TimeSpan.FromMilliseconds(250));
        await WaitForMarkerClearedAsync(harness.Store);
        await WaitForRecorderLineAsync(harness, "direct-paddle-bench-stop-observed");

        Assert.Equal(PHprDirectRuntimeState.Idle, harness.Runtime.GetSnapshot().State);
        Assert.Contains(harness.RecorderLines(), line => line.Contains("\"InterPressIntervalMs\":10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BenchDropsStalePaddlePulseInsteadOfPlayingLate()
    {
        using var harness = new RuntimeHarness();
        harness.Runtime.Configure(harness.ReadyEnvironment());
        await harness.Runtime.InitializeStartupCleanupAsync();
        harness.Writer.Clear();
        var stalePaddleUtc = harness.RuntimeClock.UtcNow.AddMilliseconds(-81);

        var message = await harness.Runtime.RouteBenchAsync(
            BenchResult(sequence: 1, timestampUtc: stalePaddleUtc),
            BenchOptions(),
            PaddleSnapshot(sequence: 1, timestampUtc: stalePaddleUtc),
            module => Card(durationMs: 40),
            DirectSafetyContext());

        Assert.Contains("stale", message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Writer.Reports);
        Assert.Equal(1, harness.Output.GetDiagnostics().StaleOutputDroppedCount);
        Assert.Contains(harness.RecorderLines(), line => line.Contains("direct-paddle-bench-rejected", StringComparison.Ordinal)
            && line.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }

    private static PaddleGearBenchTestOptions BenchOptions()
    {
        return new PaddleGearBenchTestOptions
        {
            IsEnabled = true,
            IsArmed = true,
            OutputMode = PaddleGearBenchTestOutputMode.Direct,
            TargetModule = PHprGearPulseTarget.Brake
        }.Normalize();
    }

    private static PaddleGearBenchTestResult BenchResult(
        InputButtonState buttonState = InputButtonState.Pressed,
        long sequence = 1,
        DateTimeOffset? timestampUtc = null)
    {
        var paddleEvent = PaddleEvent(buttonState, sequence, timestampUtc);
        return PaddleGearBenchTestResult.AcceptedEvent(
            paddleEvent,
            BenchOptions(),
            ShiftIntentEvent.CreatePaddlePress(
                PaddleSide.Right,
                HapticDrive.Input.Abstractions.Driving.DrivingArmedState.Armed("test"),
                paddleEvent.TimestampUtc,
                paddleEvent.SequenceNumber,
                paddleEvent.SourceDevice?.DeviceId,
                lastTelemetryGear: null,
                ShiftIntentDirection.Upshift,
                ShiftIntentSource.Test,
                ShiftIntentMode.InstantPaddleOnly,
                paddleEvent.StopwatchTicks,
                paddleEvent.ButtonId,
                acceptedAtUtc: paddleEvent.TimestampUtc.AddMilliseconds(1)),
            paddleEvent.TimestampUtc.AddMilliseconds(1));
    }

    private static WheelPaddleInputSnapshot PaddleSnapshot(long sequence = 1, DateTimeOffset? timestampUtc = null)
    {
        var paddleEvent = PaddleEvent(sequence: sequence, timestampUtc: timestampUtc);
        return new WheelPaddleInputSnapshot(
            InputListenerStatus.Listening,
            Selection(),
            new WheelPaddleMapping { LeftPaddleButtonId = 14, RightPaddleButtonId = 13 },
            InputButtonState.Released,
            InputButtonState.Pressed,
            13,
            InputButtonState.Pressed,
            paddleEvent,
            1,
            0,
            null,
            paddleEvent.TimestampUtc);
    }

    private static WheelPaddleInputEvent PaddleEvent(
        InputButtonState buttonState = InputButtonState.Pressed,
        long sequence = 1,
        DateTimeOffset? timestampUtc = null)
    {
        return new WheelPaddleInputEvent(
            PaddleSide.Right,
            Selection(),
            13,
            new InputEventTimestamp(timestampUtc ?? new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero), 12345 + sequence),
            sequence,
            buttonState);
    }

    private static InputDeviceSelection Selection()
    {
        return new InputDeviceSelection(
            "windowsgamecontroller:vid_3670_pid_0905",
            "Synthetic GT Neo 32-button device",
            InputDiscoveryMethod.WindowsGameController,
            NativeDeviceIndex: 0,
            ButtonCount: 32);
    }

    private static PHprRealGearPulseSettings Card(int durationMs)
    {
        return PHprRealGearPulseSettings.Default with
        {
            IsEnabled = true,
            Strength01 = 0.10d,
            FrequencyHz = 50d,
            DurationMs = durationMs
        };
    }

    private static PHprSafetyContext DirectSafetyContext()
    {
        return PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            IsDeviceConnected = true,
            BrakeModuleAvailable = true,
            ThrottleModuleAvailable = true,
            DrivingArmed = true,
            TelemetryStale = false,
            HapticsStopped = false,
            RequiresRealDeviceWrites = true,
            SoftwareConflictStatus = PHprSoftwareConflictStatus.Clear
        };
    }

    private static PHprHidDeviceSelector ReadySelector(string path = "synthetic-feature-report-device")
    {
        return new PHprHidDeviceSelector(
            path,
            "Synthetic P-HPR",
            "Synthetic feature report interface",
            PHprDirectOutputCandidate.F1EcFeatureReportId,
            SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            PHprHidReportTransport.FeatureReport);
    }

    private static PHprRealOutputOptions ReadyOptions(PHprHidDeviceSelector selector)
    {
        return PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            DirectControlApprovalConfirmed = true,
            CandidateSourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            CandidateIsRawInputOnly = false,
            CandidateHasOpenableHidPath = true,
            CandidateFeatureReportCapabilityKnown = true,
            CandidateOutputReportCapabilityKnown = false,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = true,
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true,
            Selector = selector,
            BrakeGearPulse = Card(durationMs: 40),
            ThrottleGearPulse = Card(durationMs: 40)
        };
    }

    private static async Task WaitForReportsAsync(FakeHidReportWriter writer, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (writer.Reports.Count >= count)
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.True(writer.Reports.Count >= count, $"Expected {count} reports but saw {writer.Reports.Count}.");
    }

    private static async Task WaitForMarkerClearedAsync(IPHprBenchUncleanShutdownStore store)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!store.Exists())
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.False(store.Exists());
    }

    private static async Task WaitForDiagnosticsAsync(
        SimagicPhprOutputDevice device,
        Func<PHprRealOutputDiagnostics, bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate(device.GetDiagnostics()))
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.True(predicate(device.GetDiagnostics()), "Expected diagnostics predicate to become true.");
    }

    private static async Task WaitForRecorderLineAsync(RuntimeHarness harness, string text)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (harness.RecorderLines().Any(line => line.Contains(text, StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.Contains(harness.RecorderLines(), line => line.Contains(text, StringComparison.Ordinal));
    }

    private sealed class RuntimeHarness : IDisposable
    {
        private readonly TempDirectory _directory = new();

        public RuntimeHarness(
            bool createMarkerBeforeRuntime = false,
            Func<PHprDirectSharedPathProof>? sharedProofFactory = null,
            PHprHidDeviceSelector? selector = null)
        {
            Selector = selector ?? ReadySelector();
            Writer = new FakeHidReportWriter(Selector);
            OutputClock = new FakeDirectStopClock();
            RuntimeClock = new FakeRuntimeClock();
            Output = new SimagicPhprOutputDevice(Writer, ReadyOptions(Selector), stopClock: OutputClock);
            PulseService = new PhprDeviceCardPulseService(Output);
            Dispatcher = new PHprDirectCommandDispatcher(PulseService, Output);
            Recorder = new FilePHprBenchFlightRecorder(_directory.Path);
            Store = new FilePHprBenchUncleanShutdownStore(_directory.Path);
            if (createMarkerBeforeRuntime)
            {
                Assert.True(Store.TryCreate("preexisting test marker", out _));
            }

            Runtime = new PHprDirectRuntimeCoordinator(
                Output,
                PulseService,
                Dispatcher,
                Recorder,
                Store,
                RuntimeClock,
                "test-commit",
                sharedProofFactory);
        }

        public PHprHidDeviceSelector Selector { get; }

        public FakeHidReportWriter Writer { get; }

        public FakeDirectStopClock OutputClock { get; }

        public FakeRuntimeClock RuntimeClock { get; }

        public SimagicPhprOutputDevice Output { get; }

        public PhprDeviceCardPulseService PulseService { get; }

        public PHprDirectCommandDispatcher Dispatcher { get; }

        public FilePHprBenchFlightRecorder Recorder { get; }

        public FilePHprBenchUncleanShutdownStore Store { get; }

        public PHprDirectRuntimeCoordinator Runtime { get; }

        public PHprDirectRuntimeEnvironment ReadyEnvironment()
        {
            return new PHprDirectRuntimeEnvironment(
                ReadyOptions(Selector) with { GearPulseRetriggerMode = PHprGearPulseRetriggerMode.RetriggerLatestPressWins },
                PHprSoftwareConflictStatus.Clear,
                RoadVibrationEnabled: false,
                SlipLockEnabled: false,
                BenchEnabled: true,
                PHprGearPulseTarget.Brake,
                "Synthetic GT Neo 32-button device",
                DebounceSuppressedCount: 0);
        }

        public string[] RecorderLines()
        {
            if (!File.Exists(Recorder.LogPath))
            {
                return [];
            }

            using var stream = new FileStream(Recorder.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        }

        public void Dispose()
        {
            _directory.Dispose();
        }
    }

    private sealed class FakeUiRefresh
    {
        public bool Updated { get; private set; }

        public void UpdateRealPhprDirectControlStatus()
        {
            Updated = true;
        }
    }

    private sealed class FakeMainWindowUiDispatcher(bool hasAccess) : IMainWindowUiDispatcher
    {
        private readonly Queue<Action> _pending = [];
        private bool _hasAccess = hasAccess;

        public bool CheckAccess()
        {
            return _hasAccess;
        }

        public void BeginInvoke(Action action)
        {
            _pending.Enqueue(action);
        }

        public ValueTask InvokeAsync(Action action)
        {
            var previous = _hasAccess;
            _hasAccess = true;
            try
            {
                action();
                return ValueTask.CompletedTask;
            }
            finally
            {
                _hasAccess = previous;
            }
        }

        public void RunNextOnUiThread()
        {
            var previous = _hasAccess;
            _hasAccess = true;
            try
            {
                _pending.Dequeue().Invoke();
            }
            finally
            {
                _hasAccess = previous;
            }
        }
    }

    private sealed class FakeHidReportWriter(PHprHidDeviceSelector selector) : IPhprHidReportWriter
    {
        private readonly List<PHprHidReport> _reports = [];

        public PHprHidDeviceSelector Selector { get; } = selector;

        public bool IsOpen { get; private set; }

        public Action<PHprHidReport>? BeforeWrite { get; set; }

        public IReadOnlyList<PHprHidReport> Reports => _reports;

        public ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = true;
            return ValueTask.FromResult(PHprHidWriteResult.Success(Selector.ReportLength, "fake open"));
        }

        public ValueTask<PHprHidWriteResult> WriteReportAsync(
            PHprHidReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeforeWrite?.Invoke(report);
            _reports.Add(report);
            return ValueTask.FromResult(PHprHidWriteResult.Success(report.Length, "fake write"));
        }

        public ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = false;
            return ValueTask.FromResult(PHprHidWriteResult.Success(0, "fake close"));
        }

        public void Clear()
        {
            _reports.Clear();
        }
    }

    private sealed class FakeDirectStopClock : IPHprDirectStopClock
    {
        private readonly object _gate = new();
        private readonly List<ScheduledDelay> _delays = [];
        private TimeSpan _elapsed;

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
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

    private sealed class FakeRuntimeClock : IPHprDirectRuntimeClock
    {
        private readonly object _gate = new();
        private readonly List<ScheduledDelay> _delays = [];
        private TimeSpan _elapsed;

        public DateTimeOffset UtcNow { get; private set; } = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

        public long MonotonicTimestamp => _elapsed.Ticks;

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

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "HapticDrive.Asio.App.Tests",
            Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
