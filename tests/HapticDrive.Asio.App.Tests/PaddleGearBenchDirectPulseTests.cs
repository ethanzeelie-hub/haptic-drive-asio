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

        var commands = PaddleGearBenchDirectPulsePlanner.BuildCommands(
            Shift(),
            PHprGearPulseTarget.Brake,
            brake,
            PHprRealGearPulseSettings.Default);

        var result = await harness.Device.SendAsync(Assert.Single(commands));

        Assert.True(result.Succeeded, result.Message);
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

        var commands = PaddleGearBenchDirectPulsePlanner.BuildCommands(
            Shift(),
            PHprGearPulseTarget.Throttle,
            PHprRealGearPulseSettings.Default,
            throttle);

        var result = await harness.Device.SendAsync(Assert.Single(commands));

        Assert.True(result.Succeeded, result.Message);
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

        var commands = PaddleGearBenchDirectPulsePlanner.BuildCommands(
            Shift(),
            PHprGearPulseTarget.Both,
            brake,
            throttle);

        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, command => command.TargetModule == PHprModuleId.Brake
            && Math.Abs(command.Strength01 - 0.11d) < 0.000001d
            && Math.Abs(command.FrequencyHz - 41d) < 0.000001d
            && command.DurationMs == 40);
        Assert.Contains(commands, command => command.TargetModule == PHprModuleId.Throttle
            && Math.Abs(command.Strength01 - 0.22d) < 0.000001d
            && Math.Abs(command.FrequencyHz - 49d) < 0.000001d
            && command.DurationMs == 55);

        foreach (var command in commands)
        {
            var result = await harness.Device.SendAsync(command);
            Assert.True(result.Succeeded, result.Message);
        }

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
