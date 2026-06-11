using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App.Tests;

public sealed class PaddleGearBenchDirectPulseTests
{
    [Fact]
    public async Task DirectBenchBrakePulseSendsStartThenStopAfterDuration()
    {
        var harness = new DirectBenchHarness();
        var brake = PHprRealGearPulseSettings.Default with { Strength01 = 0.11d, FrequencyHz = 41d, DurationMs = 40 };

        var result = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);

        Assert.True(result.Succeeded, result.CommandResult.Message);
        Assert.Equal(PhprDeviceCardPulseService.RouteName, result.RouteName);
        Assert.Single(harness.Writer.Reports);
        Assert.Equal(PHprHidReportState.Start, harness.Writer.Reports[0].State);
        Assert.Equal(PHprModuleId.Brake, harness.Writer.Reports[0].TargetModule);
        Assert.True(harness.Device.GetDiagnostics().ActivePulse);
        await WaitForScheduledDelayAsync(harness.Clock, 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(39));
        Assert.Single(harness.Writer.Reports);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
        await WaitForReportsAsync(harness.Writer, 2);
        await WaitForNoPendingStopsAsync(harness.Device);
        var diagnostics = harness.Device.GetDiagnostics();
        Assert.Equal(PHprHidReportState.Stop, harness.Writer.Reports[1].State);
        Assert.Equal(PHprModuleId.Brake, harness.Writer.Reports[1].TargetModule);
        Assert.False(diagnostics.ActivePulse);
        Assert.Equal(0, diagnostics.Output.PendingScheduledStopCount);
        Assert.Equal(40, diagnostics.LastScheduledPulseDurationMs);
        Assert.NotNull(diagnostics.LastStartSentAtUtc);
        Assert.NotNull(diagnostics.LastStopSentAtUtc);
        Assert.Equal(PHprModuleId.Brake, diagnostics.LastStartReportTarget);
        Assert.Equal(PHprModuleId.Brake, diagnostics.LastStopReportTarget);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.LastStopResultStatus);
    }

    [Fact]
    public async Task DirectBenchThrottlePulseSendsStartThenStopAfterDuration()
    {
        var harness = new DirectBenchHarness();
        var throttle = PHprRealGearPulseSettings.Default with { Strength01 = 0.22d, FrequencyHz = 49d, DurationMs = 35 };

        var result = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Throttle,
            throttle,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);

        Assert.True(result.Succeeded, result.CommandResult.Message);
        Assert.Equal(PhprDeviceCardPulseService.RouteName, result.RouteName);
        Assert.Single(harness.Writer.Reports);
        Assert.Equal(PHprHidReportState.Start, harness.Writer.Reports[0].State);
        Assert.Equal(PHprModuleId.Throttle, harness.Writer.Reports[0].TargetModule);
        await WaitForScheduledDelayAsync(harness.Clock, 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(35));
        await WaitForReportsAsync(harness.Writer, 2);
        await WaitForNoPendingStopsAsync(harness.Device);
        Assert.Equal(PHprHidReportState.Stop, harness.Writer.Reports[1].State);
        Assert.Equal(PHprModuleId.Throttle, harness.Writer.Reports[1].TargetModule);
        Assert.False(harness.Device.GetDiagnostics().ActivePulse);
    }

    [Fact]
    public async Task DirectBenchBothTargetSendsBothStartsAndBothStopsUsingDeviceCardValues()
    {
        var harness = new DirectBenchHarness();
        var brake = PHprRealGearPulseSettings.Default with { Strength01 = 0.11d, FrequencyHz = 41d, DurationMs = 40 };
        var throttle = PHprRealGearPulseSettings.Default with { Strength01 = 0.22d, FrequencyHz = 49d, DurationMs = 55 };

        var brakeResult = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);
        var throttleResult = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Throttle,
            throttle,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);

        Assert.True(brakeResult.Succeeded, brakeResult.CommandResult.Message);
        Assert.True(throttleResult.Succeeded, throttleResult.CommandResult.Message);
        Assert.Equal(PHprModuleId.Brake, brakeResult.Command.TargetModule);
        Assert.Equal(0.11d, brakeResult.Command.Strength01, precision: 6);
        Assert.Equal(41d, brakeResult.Command.FrequencyHz, precision: 6);
        Assert.Equal(40, brakeResult.Command.DurationMs);
        Assert.Equal(PHprModuleId.Throttle, throttleResult.Command.TargetModule);
        Assert.Equal(0.22d, throttleResult.Command.Strength01, precision: 6);
        Assert.Equal(49d, throttleResult.Command.FrequencyHz, precision: 6);
        Assert.Equal(55, throttleResult.Command.DurationMs);

        Assert.Equal(2, harness.Writer.Reports.Count);
        Assert.Equal(2, harness.Writer.Reports.Count(report => report.State == PHprHidReportState.Start));
        await WaitForScheduledDelayAsync(harness.Clock, 2);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(55));
        await WaitForReportsAsync(harness.Writer, 4);
        await WaitForNoPendingStopsAsync(harness.Device);
        Assert.Equal(2, harness.Writer.Reports.Count(report => report.State == PHprHidReportState.Stop));
        Assert.Contains(harness.Writer.Reports, report => report.State == PHprHidReportState.Stop && report.TargetModule == PHprModuleId.Brake);
        Assert.Contains(harness.Writer.Reports, report => report.State == PHprHidReportState.Stop && report.TargetModule == PHprModuleId.Throttle);
        Assert.False(harness.Device.GetDiagnostics().ActivePulse);
    }

    [Fact]
    public async Task DirectBenchRetriggerIgnoresOldStopAndLatestStopSucceeds()
    {
        var harness = new DirectBenchHarness();
        var brake = PHprRealGearPulseSettings.Default with { Strength01 = 0.20d, FrequencyHz = 50d, DurationMs = 40 };

        var first = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);
        await WaitForScheduledDelayAsync(harness.Clock, 1);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(10));
        var second = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc.AddMilliseconds(10));
        await WaitForScheduledDelayAsync(harness.Clock, 2);

        Assert.True(first.Succeeded, first.CommandResult.Message);
        Assert.True(second.Succeeded, second.CommandResult.Message);
        Assert.Equal(2, harness.Writer.Reports.Count(report => report.State == PHprHidReportState.Start));
        Assert.Equal(2, harness.Device.GetDiagnostics().BrakePulseGeneration);
        Assert.Equal(1, harness.Device.GetDiagnostics().RetriggerCount);

        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(30));
        await WaitForDiagnosticsAsync(harness.Device, diagnostics => diagnostics.StaleStopIgnoredCount == 1);
        Assert.Equal(2, harness.Writer.Reports.Count);
        Assert.True(harness.Device.GetDiagnostics().ActivePulse);

        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(10));
        await WaitForReportsAsync(harness.Writer, 3);
        await WaitForNoPendingStopsAsync(harness.Device);

        var diagnostics = harness.Device.GetDiagnostics();
        Assert.Equal(1, diagnostics.StaleStopIgnoredCount);
        Assert.Equal(PHprModuleId.Brake, diagnostics.LastStaleStopTarget);
        Assert.Equal(PHprHidReportState.Stop, harness.Writer.Reports[2].State);
        Assert.Equal(PHprModuleId.Brake, harness.Writer.Reports[2].TargetModule);
        Assert.False(diagnostics.ActivePulse);
    }

    [Fact]
    public async Task DirectBenchBothTargetRetriggersBrakeAndThrottleIndependently()
    {
        var harness = new DirectBenchHarness();
        var brake = PHprRealGearPulseSettings.Default with { Strength01 = 0.20d, FrequencyHz = 50d, DurationMs = 40 };
        var throttle = PHprRealGearPulseSettings.Default with { Strength01 = 0.20d, FrequencyHz = 50d, DurationMs = 60 };

        _ = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);
        _ = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Throttle,
            throttle,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);
        await WaitForScheduledDelayAsync(harness.Clock, 2);

        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(10));
        _ = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc.AddMilliseconds(10));
        await WaitForScheduledDelayAsync(harness.Clock, 3);

        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(30));
        await WaitForDiagnosticsAsync(harness.Device, diagnostics => diagnostics.StaleStopIgnoredCount == 1);
        Assert.DoesNotContain(harness.Writer.Reports, report => report.State == PHprHidReportState.Stop);

        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(20));
        await WaitForReportsAsync(harness.Writer, 5);

        var stops = harness.Writer.Reports.Where(report => report.State == PHprHidReportState.Stop).ToArray();
        Assert.Contains(stops, report => report.TargetModule == PHprModuleId.Brake);
        Assert.Contains(stops, report => report.TargetModule == PHprModuleId.Throttle);
        Assert.Equal(2, harness.Device.GetDiagnostics().BrakePulseGeneration);
        Assert.Equal(1, harness.Device.GetDiagnostics().ThrottlePulseGeneration);
    }

    [Fact]
    public async Task StopAllOverridesGenerationsAndPendingStops()
    {
        var harness = new DirectBenchHarness();
        var brake = PHprRealGearPulseSettings.Default with { Strength01 = 0.20d, FrequencyHz = 50d, DurationMs = 40 };

        _ = await PhprDeviceCardPulseService.SendDirectPulseAsync(
            harness.Device,
            PHprModuleId.Brake,
            brake,
            PHprSafetyContextForDirectBench(),
            Shift().TimestampUtc);
        await WaitForScheduledDelayAsync(harness.Clock, 1);

        var stopAll = await harness.Device.StopAllAsync();

        Assert.True(stopAll.Succeeded, stopAll.Message);
        Assert.False(harness.Device.GetDiagnostics().ActivePulse);
        Assert.Equal(0, harness.Device.GetDiagnostics().Output.PendingScheduledStopCount);
        harness.Clock.AdvanceBy(TimeSpan.FromMilliseconds(40));
        Assert.Equal(3, harness.Writer.Reports.Count);
        Assert.Equal(2, harness.Writer.Reports.Count(report => report.State == PHprHidReportState.Stop));
    }

    [Fact]
    public void ConstructingDirectBenchOutputDoesNotWriteOnStartup()
    {
        var harness = new DirectBenchHarness();

        var diagnostics = harness.Device.GetDiagnostics();

        Assert.Empty(harness.Writer.Reports);
        Assert.Equal(0, diagnostics.ReportWriteCount);
        Assert.False(diagnostics.ActivePulse);
        Assert.Equal(0, diagnostics.Output.PendingScheduledStopCount);
    }

    private static ShiftIntentEvent Shift()
    {
        return ShiftIntentEvent.CreatePaddlePress(
            PaddleSide.Right,
            DrivingArmedState.Armed("bench test"),
            timestampUtc: new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
    }

    private static PHprRealOutputOptions ReadyOptions()
    {
        return PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            DirectControlApprovalConfirmed = true,
            CandidateIsRawInputOnly = false,
            CandidateHasOpenableHidPath = true,
            CandidateFeatureReportCapabilityKnown = true,
            CandidateOutputReportCapabilityKnown = false,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = true,
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true,
            Selector = ReadySelector()
        };
    }

    private static PHprHidDeviceSelector ReadySelector()
    {
        return new PHprHidDeviceSelector(
            "sanitized-device-path",
            "Synthetic P-HPR",
            "Synthetic feature report interface",
            PHprDirectOutputCandidate.F1EcFeatureReportId,
            SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            PHprHidReportTransport.FeatureReport);
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

    private static async Task WaitForScheduledDelayAsync(FakeDirectStopClock clock, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (clock.ScheduledDelayCount >= count)
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.True(clock.ScheduledDelayCount >= count, $"Expected {count} scheduled delay(s) but saw {clock.ScheduledDelayCount}.");
    }

    private static async Task WaitForNoPendingStopsAsync(SimagicPhprOutputDevice device)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (device.GetDiagnostics().Output.PendingScheduledStopCount == 0)
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.Equal(0, device.GetDiagnostics().Output.PendingScheduledStopCount);
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

    private sealed class DirectBenchHarness
    {
        public DirectBenchHarness()
        {
            Writer = new FakeHidReportWriter(ReadySelector());
            Clock = new FakeDirectStopClock();
            Device = new SimagicPhprOutputDevice(Writer, ReadyOptions(), stopClock: Clock);
            Device.SetSafetyContext(PHprSafetyContextForDirectBench());
        }

        public FakeHidReportWriter Writer { get; }

        public FakeDirectStopClock Clock { get; }

        public SimagicPhprOutputDevice Device { get; }
    }

    private static HapticDrive.Simagic.PHPR.Abstractions.Safety.PHprSafetyContext PHprSafetyContextForDirectBench()
    {
        return HapticDrive.Simagic.PHPR.Abstractions.Safety.PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            IsDeviceConnected = true,
            BrakeModuleAvailable = true,
            ThrottleModuleAvailable = true,
            TelemetryStale = false,
            HapticsStopped = false,
            DrivingArmed = true,
            RequiresRealDeviceWrites = true
        };
    }

    private sealed class FakeHidReportWriter(PHprHidDeviceSelector selector) : IPhprHidReportWriter
    {
        private readonly List<PHprHidReport> _reports = [];

        public PHprHidDeviceSelector Selector { get; } = selector;

        public bool IsOpen { get; private set; }

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
            _reports.Add(report);
            return ValueTask.FromResult(PHprHidWriteResult.Success(report.Length, "fake write"));
        }

        public ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = false;
            return ValueTask.FromResult(PHprHidWriteResult.Success(0, "fake close"));
        }
    }

    private sealed class FakeDirectStopClock : IPHprDirectStopClock
    {
        private readonly object _gate = new();
        private readonly List<ScheduledDelay> _delays = [];
        private TimeSpan _elapsed;

        public int ScheduledDelayCount
        {
            get
            {
                lock (_gate)
                {
                    return _delays.Count;
                }
            }
        }

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask(Task.FromCanceled(cancellationToken));
            }

            lock (_gate)
            {
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
                ready = _delays
                    .Where(scheduled => scheduled.DueAt <= _elapsed)
                    .ToArray();
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

        private sealed class ScheduledDelay : IDisposable
        {
            private readonly TaskCompletionSource _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenRegistration _registration;

            public ScheduledDelay(TimeSpan dueAt, CancellationToken cancellationToken)
            {
                DueAt = dueAt;
                _registration = cancellationToken.Register(() =>
                {
                    _completion.TrySetCanceled(cancellationToken);
                });
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
    }
}
