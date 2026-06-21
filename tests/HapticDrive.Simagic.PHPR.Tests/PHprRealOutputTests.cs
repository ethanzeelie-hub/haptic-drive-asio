using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprRealOutputTests
{
    [Fact]
    public async Task NoRealWriteWithoutEnabledAndArmed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, PHprRealOutputOptions.Disabled with
        {
            Selector = SelectedDevice()
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public void NoRealWriteOnStartup()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        _ = new SimagicPhprOutputDevice(writer);

        Assert.Empty(writer.Reports);
    }

    [Fact]
    public void DisabledRealOutputOptionsDefaultOffUnarmedAndUnselected()
    {
        var options = PHprRealOutputOptions.Disabled.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);

        Assert.False(options.DirectControlEnabled);
        Assert.False(options.DirectControlArmed);
        Assert.False(options.Selector.IsSelected);
        Assert.Equal(PHprRealOutputOptions.DefaultWriteTimeoutMs, options.WriteTimeoutMs);
    }

    [Fact]
    public async Task NoWriteWhenDeviceIsNotSelected()
    {
        var writer = new FakeHidReportWriter(PHprHidDeviceSelector.None);
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions() with
        {
            Selector = PHprHidDeviceSelector.None
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task DirectPulseDoesNotRequireApprovalPhrase()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions() with
        {
            DirectControlApprovalConfirmed = false
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, writer.OpenCount);
        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.Start);
    }

    [Theory]
    [InlineData(PHprSoftwareConflictStatus.Unknown)]
    [InlineData(PHprSoftwareConflictStatus.SimProRunning)]
    [InlineData(PHprSoftwareConflictStatus.SimHubRunning)]
    [InlineData(PHprSoftwareConflictStatus.ActiveConflict)]
    public async Task NoWriteWhenSoftwareCoexistenceIsNotClear(PHprSoftwareConflictStatus status)
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        device.SetSafetyContext(PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            RequiresRealDeviceWrites = true,
            SoftwareConflictStatus = status
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task NoWriteWhenSafetyRejects()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        device.SetSafetyContext(PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            RequiresRealDeviceWrites = true,
            TelemetryStale = true
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task FakeWriterReceivesSimHubBrakeStartAndStop()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 1));
        await WaitForReportsAsync(writer, 2);

        Assert.True(result.Succeeded);
        Assert.Equal(1, writer.OpenCount);
        Assert.Equal(PHprModuleId.Brake, writer.Reports[0].TargetModule);
        Assert.Equal([0xF1, 0xEC, 0x01, 0x01, 0x32, 0x0A], writer.Reports[0].Payload.Take(6).ToArray());
        Assert.Equal([0xF1, 0xEC, 0x01, 0x00, 0x0A, 0x00], writer.Reports[1].Payload.Take(6).ToArray());
    }

    [Fact]
    public async Task FakeWriterReceivesSimHubThrottleStartAndStop()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        var result = await device.SendAsync(TestCommand(PHprModuleId.Throttle, durationMs: 1));
        await WaitForReportsAsync(writer, 2);

        Assert.True(result.Succeeded);
        Assert.Equal(PHprModuleId.Throttle, writer.Reports[0].TargetModule);
        Assert.Equal([0xF1, 0xEC, 0x02, 0x01, 0x32, 0x0A], writer.Reports[0].Payload.Take(6).ToArray());
        Assert.Equal([0xF1, 0xEC, 0x02, 0x00, 0x0A, 0x00], writer.Reports[1].Payload.Take(6).ToArray());
    }

    [Fact]
    public async Task FakeClockBrakePulseSendsStopAfterDuration()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 40));

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(writer.Reports);
        Assert.Equal(PHprHidReportState.Start, writer.Reports[0].State);
        await WaitForScheduledDelayAsync(clock, 1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(39));
        Assert.Single(writer.Reports);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
        await WaitForReportsAsync(writer, 2);
        Assert.Equal(PHprHidReportState.Stop, writer.Reports[1].State);
        Assert.Equal([0xF1, 0xEC, 0x01, 0x00, 0x0A, 0x00], writer.Reports[1].Payload.Take(6).ToArray());
    }

    [Fact]
    public async Task FakeClockThrottlePulseSendsStopAfterDuration()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        var result = await device.SendAsync(TestCommand(PHprModuleId.Throttle, durationMs: 35));

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(writer.Reports);
        Assert.Equal(PHprHidReportState.Start, writer.Reports[0].State);
        await WaitForScheduledDelayAsync(clock, 1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(35));
        await WaitForReportsAsync(writer, 2);
        Assert.Equal(PHprHidReportState.Stop, writer.Reports[1].State);
        Assert.Equal([0xF1, 0xEC, 0x02, 0x00, 0x0A, 0x00], writer.Reports[1].Payload.Take(6).ToArray());
    }

    [Fact]
    public async Task FakeClockBothTargetSendsBothStartsAndBothStops()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        var result = await device.SendAsync(TestCommand(PHprModuleId.Both, durationMs: 25));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, writer.Reports.Count);
        Assert.Equal([PHprModuleId.Brake, PHprModuleId.Throttle], writer.Reports.Select(report => report.TargetModule).ToArray());
        Assert.All(writer.Reports, report => Assert.Equal(PHprHidReportState.Start, report.State));
        await WaitForScheduledDelayAsync(clock, 1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(25));
        await WaitForReportsAsync(writer, 4);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.Stop));
        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.Stop && report.TargetModule == PHprModuleId.Brake);
        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.Stop && report.TargetModule == PHprModuleId.Throttle);
    }

    [Fact]
    public async Task EmergencyStopSendsImmediateStopsAndCancelsPendingDurationStop()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 100));
        await WaitForScheduledDelayAsync(clock, 1);
        Assert.True(device.GetDiagnostics().ActivePulse);
        await device.EmergencyStopAsync();
        clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await Task.Yield();

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(3, writer.Reports.Count);
        Assert.Single(writer.Reports, report => report.State == PHprHidReportState.Start);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.EmergencyStop));
        Assert.Equal(0, device.GetSnapshot().PendingScheduledStopCount);
        Assert.False(device.GetDiagnostics().ActivePulse);
    }

    [Fact]
    public async Task NoPulseRemainsActiveAfterConfiguredDuration()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 20));
        await WaitForScheduledDelayAsync(clock, 1);
        Assert.Equal(1, device.GetSnapshot().PendingScheduledStopCount);
        Assert.True(device.GetDiagnostics().ActivePulse);

        clock.AdvanceBy(TimeSpan.FromMilliseconds(20));
        await WaitForReportsAsync(writer, 2);

        Assert.Equal(0, device.GetSnapshot().PendingScheduledStopCount);
        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.Stop);
        Assert.False(device.GetDiagnostics().ActivePulse);
        Assert.Equal(PHprHidWriteStatus.Succeeded, device.GetDiagnostics().LastStopResultStatus);
    }

    [Fact]
    public async Task WatchdogSendsStopAllWhenScheduledStopFails()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        var start = await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 20));
        writer.NextWriteResults.Enqueue(PHprHidWriteResult.Failure("scheduled stop failed"));

        Assert.True(start.Succeeded, start.Message);
        await WaitForScheduledDelayAsync(clock, 1);
        clock.AdvanceBy(TimeSpan.FromMilliseconds(20));
        await WaitForWriteAttemptsAsync(writer, 2);
        Assert.True(device.GetDiagnostics().ActivePulse);

        clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
        await WaitForWriteAttemptsAsync(writer, 4);
        var diagnostics = device.GetDiagnostics();

        Assert.False(diagnostics.ActivePulse);
        Assert.True(diagnostics.Output.IsEmergencyStopActive);
        Assert.Equal(1, diagnostics.WatchdogStopAllCount);
        Assert.Contains(writer.WriteAttempts, report => report.State == PHprHidReportState.EmergencyStop && report.TargetModule == PHprModuleId.Brake);
        Assert.Contains(writer.WriteAttempts, report => report.State == PHprHidReportState.EmergencyStop && report.TargetModule == PHprModuleId.Throttle);
    }

    [Fact]
    public async Task EmergencyStopRetriesAndAttemptsBrakeAndThrottleWhenFirstStopFails()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 100));
        await WaitForScheduledDelayAsync(clock, 1);
        writer.NextWriteResults.Enqueue(PHprHidWriteResult.Failure("brake emergency stop failed"));

        await device.EmergencyStopAsync();
        var diagnostics = device.GetDiagnostics();

        Assert.False(diagnostics.ActivePulse);
        Assert.True(diagnostics.Output.IsEmergencyStopActive);
        Assert.NotNull(diagnostics.LastEmergencyStopRequestedAtUtc);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.LastEmergencyStopResultStatus);
        Assert.Contains(writer.WriteAttempts, report => report.State == PHprHidReportState.EmergencyStop && report.TargetModule == PHprModuleId.Brake);
        Assert.Contains(writer.WriteAttempts, report => report.State == PHprHidReportState.EmergencyStop && report.TargetModule == PHprModuleId.Throttle);
        Assert.True(writer.WriteAttempts.Count(report => report.State == PHprHidReportState.EmergencyStop) >= 3);
    }

    [Fact]
    public async Task ExplicitOpenAndCloseUpdateConnectionDiagnosticsWithoutReports()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        var open = await device.OpenAsync();
        var close = await device.CloseAsync();
        var diagnostics = device.GetDiagnostics();

        Assert.True(open.Succeeded);
        Assert.True(close.Succeeded);
        Assert.Equal(1, writer.OpenCount);
        Assert.Equal(1, writer.CloseCount);
        Assert.Empty(writer.Reports);
        Assert.Equal(PHprHidConnectionState.Closed, diagnostics.Connection.State);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.Connection.LastOpenStatus);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.Connection.LastCloseStatus);
    }

    [Fact]
    public async Task ExplicitOpenRequiresEnabledArmedAndSelectedInterface()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, PHprRealOutputOptions.Disabled with
        {
            Selector = SelectedDevice()
        });

        var open = await device.OpenAsync();

        Assert.False(open.Succeeded);
        Assert.False(writer.IsOpen);
        Assert.Equal(0, writer.OpenCount);
    }

    [Fact]
    public async Task WriteFailureRecordsDiagnosticsAndRejectsCommand()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        writer.NextWriteResults.Enqueue(PHprHidWriteResult.Failure("fake write failed", "planned failure"));
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));
        var diagnostics = device.GetDiagnostics();

        Assert.False(result.Succeeded);
        Assert.Empty(writer.Reports);
        Assert.Equal(1, diagnostics.FailedReportWriteCount);
        Assert.Equal(PHprHidConnectionState.Faulted, diagnostics.Connection.State);
        Assert.Equal(PHprHidWriteStatus.Failed, diagnostics.Connection.LastWriteStatus);
        Assert.Contains("fake write failed", diagnostics.Output.LastMessage);
    }

    [Fact]
    public async Task EmergencyStopRetryRecordsFailedAttemptAndFinalSuccess()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        writer.NextWriteResults.Enqueue(PHprHidWriteResult.Failure("fake stop failed", "planned stop failure"));
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        await device.EmergencyStopAsync();
        var diagnostics = device.GetDiagnostics();

        Assert.True(diagnostics.Output.IsEmergencyStopActive);
        Assert.Equal(PHprHidConnectionState.Open, diagnostics.Connection.State);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.Connection.LastStopStatus);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.LastEmergencyStopResultStatus);
        Assert.Contains("attempt 2", diagnostics.LastEmergencyStopResultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, diagnostics.FailedReportWriteCount);
        Assert.Contains("planned stop failure", diagnostics.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisconnectedWriterMarksOutputDisconnectedAndBlocksNextStartThroughSafety()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        writer.NextWriteResults.Enqueue(PHprHidWriteResult.Failure("fake disconnected", status: PHprHidWriteStatus.Disconnected));
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        var first = await device.SendAsync(TestCommand(PHprModuleId.Brake));
        var second = await device.SendAsync(TestCommand(PHprModuleId.Brake));
        var diagnostics = device.GetDiagnostics();

        Assert.False(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.False(diagnostics.Output.IsConnected);
        Assert.Equal(PHprHidConnectionState.Disconnected, diagnostics.Connection.State);
        Assert.Equal(1, diagnostics.Connection.DisconnectCount);
        Assert.Single(writer.WriteAttempts);
    }

    [Fact]
    public async Task WriteTimeoutRecordsTimeoutAndDoesNotRecordSuccessfulReport()
    {
        var writer = new FakeHidReportWriter(SelectedDevice())
        {
            WriteDelay = TimeSpan.FromMilliseconds(100)
        };
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions() with
        {
            WriteTimeoutMs = PHprRealOutputOptions.MinWriteTimeoutMs
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));
        var diagnostics = device.GetDiagnostics();

        Assert.False(result.Succeeded);
        Assert.Empty(writer.Reports);
        Assert.Equal(PHprHidConnectionState.Faulted, diagnostics.Connection.State);
        Assert.Equal(PHprHidWriteStatus.TimedOut, diagnostics.Connection.LastWriteStatus);
        Assert.Equal(1, diagnostics.Connection.TimeoutCount);
    }

    [Fact]
    public async Task InvalidReportLengthIsRejectedBeforeOpeningWriter()
    {
        var selector = SelectedDevice() with { ReportLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes - 1 };
        var writer = new FakeHidReportWriter(selector);
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions() with
        {
            Selector = selector
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Equal(0, writer.OpenCount);
        Assert.Empty(writer.Reports);
        Assert.Contains("report length", result.Message);
    }

    [Fact]
    public async Task EmergencyStopSendsStopFrames()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        await device.EmergencyStopAsync();

        Assert.Equal(2, writer.Reports.Count);
        Assert.All(writer.Reports, report => Assert.Equal(PHprHidReportState.EmergencyStop, report.State));
        Assert.Equal(PHprModuleId.Brake, writer.Reports[0].TargetModule);
        Assert.Equal(PHprModuleId.Throttle, writer.Reports[1].TargetModule);
    }

    [Fact]
    public async Task DisposeDoesNotWriteWhenSelectedButUnarmed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, PHprRealOutputOptions.Disabled with
        {
            Selector = SelectedDevice()
        });

        await device.DisposeAsync();

        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task DisposeSendsStopFramesWhenArmedAndSelected()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        await device.DisposeAsync();
        var diagnostics = device.GetDiagnostics();

        Assert.Equal(2, writer.Reports.Count);
        Assert.Equal(1, writer.OpenCount);
        Assert.Equal(1, writer.CloseCount);
        Assert.All(writer.Reports, report => Assert.Equal(PHprHidReportState.EmergencyStop, report.State));
        Assert.Equal(PHprHidConnectionState.Disposed, diagnostics.Connection.State);
        Assert.Equal(PHprHidWriteStatus.Succeeded, diagnostics.Connection.LastStopStatus);
    }

    [Fact]
    public async Task DisposeSendsStopsIfPulseActiveAndClearsActiveState()
    {
        var writer = new FakeHidReportWriter(SelectedFeatureReportDevice());
        var clock = new FakeDirectStopClock();
        var device = new SimagicPhprOutputDevice(writer, FeatureReportOptions(), stopClock: clock);

        await device.SendAsync(TestCommand(PHprModuleId.Brake, durationMs: 100));
        await WaitForScheduledDelayAsync(clock, 1);
        Assert.True(device.GetDiagnostics().ActivePulse);

        await device.DisposeAsync();
        var diagnostics = device.GetDiagnostics();

        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.Start);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.EmergencyStop));
        Assert.Equal(0, diagnostics.Output.PendingScheduledStopCount);
        Assert.False(diagnostics.ActivePulse);
    }

    [Fact]
    public async Task GearPaddleAcceptedEventWritesOnlyWhenEnabledAndArmed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprDirectGearPulseRouter(device, ArmedOptions());

        var result = await router.RouteAsync(AcceptedShift(), RealSafetyContext());

        Assert.True(result.Routed);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.Start));
    }

    [Theory]
    [InlineData(PaddleSide.Right, ShiftIntentDirection.Upshift)]
    [InlineData(PaddleSide.Left, ShiftIntentDirection.Downshift)]
    public async Task AcceptedPaddleShiftRoutesImmediatelyWithLatencyTrace(
        PaddleSide paddleSide,
        ShiftIntentDirection expectedDirection)
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprDirectGearPulseRouter(device, ArmedOptions());
        var paddleAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-12);
        var acceptedAtUtc = paddleAtUtc.AddMilliseconds(1);
        var shift = AcceptedShift(paddleSide, paddleAtUtc, acceptedAtUtc);

        var result = await router.RouteAsync(shift, RealSafetyContext());

        Assert.True(result.Routed);
        Assert.Equal(expectedDirection, result.ShiftIntentEvent?.Direction);
        Assert.Equal(paddleAtUtc, result.PaddleEventAtUtc);
        Assert.Equal(acceptedAtUtc, result.ShiftIntentAcceptedAtUtc);
        Assert.NotNull(result.FirstCommandCreatedAtUtc);
        Assert.NotNull(result.FirstWriteCompletedAtUtc);
        Assert.True(result.FirstCommandCreatedAtUtc >= acceptedAtUtc);
        Assert.True(result.FirstWriteCompletedAtUtc >= result.FirstCommandCreatedAtUtc);
        Assert.Equal(2, result.CommandTraces?.Count);
        Assert.All(result.CommandTraces!, trace => Assert.Equal(trace.CommandCreatedAtUtc, trace.Command.TimestampUtc));
        Assert.Equal(2, writer.WriteAttempts.Count(report => report.State == PHprHidReportState.Start));
    }

    [Fact]
    public async Task UpshiftAndDownshiftUseSameDefaultPulse()
    {
        var upWriter = new FakeHidReportWriter(SelectedDevice());
        var upDevice = new SimagicPhprOutputDevice(upWriter, ArmedOptions());
        var upRouter = new PHprDirectGearPulseRouter(upDevice, ArmedOptions());
        var downWriter = new FakeHidReportWriter(SelectedDevice());
        var downDevice = new SimagicPhprOutputDevice(downWriter, ArmedOptions());
        var downRouter = new PHprDirectGearPulseRouter(downDevice, ArmedOptions());

        await upRouter.RouteAsync(AcceptedShift(PaddleSide.Right), RealSafetyContext());
        await downRouter.RouteAsync(AcceptedShift(PaddleSide.Left), RealSafetyContext());

        var upStarts = upWriter.Reports.Where(report => report.State == PHprHidReportState.Start).ToArray();
        var downStarts = downWriter.Reports.Where(report => report.State == PHprHidReportState.Start).ToArray();
        Assert.Equal(2, upStarts.Length);
        Assert.Equal(2, downStarts.Length);
        Assert.Equal(upStarts[0].Payload, downStarts[0].Payload);
        Assert.Equal(upStarts[1].Payload, downStarts[1].Payload);
    }

    [Fact]
    public async Task DirectGearPulseDisabledByDefaultDoesNotWrite()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, PHprRealOutputOptions.Disabled);
        var router = new PHprDirectGearPulseRouter(device, PHprRealOutputOptions.Disabled);

        var result = await router.RouteAsync(AcceptedShift(), RealSafetyContext());

        Assert.False(result.Routed);
        Assert.Empty(writer.Reports);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DirectGearPulseWithoutApprovalStillRoutesWhenDirectReady()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var options = ArmedOptions() with
        {
            DirectControlApprovalConfirmed = false
        };
        var device = new SimagicPhprOutputDevice(writer, options);
        var router = new PHprDirectGearPulseRouter(device, options);

        var result = await router.RouteAsync(AcceptedShift(), RealSafetyContext());

        Assert.True(result.Routed, result.Message);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.Start));
    }

    [Fact]
    public async Task DirectGearPulseSimProConflictRejectsWithoutWriting()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprDirectGearPulseRouter(device, ArmedOptions());

        var result = await router.RouteAsync(
            AcceptedShift(),
            RealSafetyContext() with { SoftwareConflictStatus = PHprSoftwareConflictStatus.SimProRunning });

        Assert.False(result.Routed);
        Assert.Empty(writer.Reports);
        Assert.Contains("SimProRunning", result.Message);
    }

    [Fact]
    public async Task DirectGearPulseBrakeOnlySuppressesThrottle()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var options = ArmedOptions() with
        {
            BrakeGearPulse = PHprRealGearPulseSettings.Default with { IsEnabled = true, DurationMs = 1 },
            ThrottleGearPulse = PHprRealGearPulseSettings.Default with { IsEnabled = false }
        };
        var device = new SimagicPhprOutputDevice(writer, options);
        var router = new PHprDirectGearPulseRouter(device, options);

        var result = await router.RouteAsync(AcceptedShift(), RealSafetyContext());

        Assert.True(result.Routed);
        var start = Assert.Single(writer.Reports, report => report.State == PHprHidReportState.Start);
        Assert.Equal(PHprModuleId.Brake, start.TargetModule);
    }

    [Fact]
    public async Task SuppressedShiftIntentDoesNotWrite()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprDirectGearPulseRouter(device, ArmedOptions());

        var result = await router.RouteAsync(SuppressedShift(), RealSafetyContext());

        Assert.False(result.Routed);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task PerPedalSettingsApplyAndDisabledPedalIsSuppressed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var options = ArmedOptions() with
        {
            BrakeGearPulse = PHprRealGearPulseSettings.Default with { IsEnabled = false },
            ThrottleGearPulse = PHprRealGearPulseSettings.Default with
            {
                IsEnabled = true,
                Strength01 = 0.07d,
                FrequencyHz = 40d,
                DurationMs = 1
            }
        };
        var device = new SimagicPhprOutputDevice(writer, options);
        var router = new PHprDirectGearPulseRouter(device, options);

        var result = await router.RouteAsync(AcceptedShift(), RealSafetyContext());

        Assert.True(result.Routed);
        var start = Assert.Single(writer.Reports, report => report.State == PHprHidReportState.Start);
        Assert.Equal(PHprModuleId.Throttle, start.TargetModule);
        Assert.Equal(0x28, start.Payload[4]);
        Assert.Equal(0x07, start.Payload[5]);
    }

    [Fact]
    public async Task RoadVibrationRoutesThroughFakeRealWriterWhenEnabledAndArmed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var roadOptions = PHprRoadVibrationRouterOptions.EnabledDefault with
        {
            Brake = PHprRoadVibrationPedalSettings.Default with { DurationMs = 1 },
            Throttle = PHprRoadVibrationPedalSettings.Default with { DurationMs = 1 }
        };
        var router = new PHprRoadVibrationRouter(device, roadOptions, device.SetSafetyContext);

        var result = await router.RouteAsync(CreateRoadVehicleState(), RealSafetyContext());
        await WaitForReportsAsync(writer, 4);

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.Start));
        Assert.Contains(writer.Reports, report => report.TargetModule == PHprModuleId.Brake && report.State == PHprHidReportState.Start);
        Assert.Contains(writer.Reports, report => report.TargetModule == PHprModuleId.Throttle && report.State == PHprHidReportState.Start);
        Assert.All(result.Commands, command => Assert.Equal(PHprCommandSource.RoadTexture, command.Command.Source));
    }

    [Fact]
    public async Task RoadVibrationSimProConflictRejectsWithoutWriting()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprRoadVibrationRouter(
            device,
            PHprRoadVibrationRouterOptions.EnabledDefault,
            device.SetSafetyContext);

        var result = await router.RouteAsync(
            CreateRoadVehicleState(),
            RealSafetyContext() with { SoftwareConflictStatus = PHprSoftwareConflictStatus.SimProRunning });

        Assert.Equal(PHprRoadVibrationRoutingStatus.RejectedBySafety, result.Status);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task SlipLockRoutesThroughFakeRealWriterWhenEnabledAndArmed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var slipLockOptions = PHprSlipLockRouterOptions.EnabledDefault with
        {
            WheelSlip = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelSlip) with { DurationMs = 1 },
            WheelLock = PHprSlipLockEffectSettings.DefaultFor(PHprPedalEffectKind.WheelLock) with { DurationMs = 1 }
        };
        var router = new PHprSlipLockRouter(device, slipLockOptions, device.SetSafetyContext);

        var result = await router.RouteAsync(CreateSlipLockVehicleState(), RealSafetyContext());
        await WaitForReportsAsync(writer, 4);

        Assert.True(result.WasRouted, result.Message);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.Start));
        Assert.Contains(writer.Reports, report => report.TargetModule == PHprModuleId.Brake && report.State == PHprHidReportState.Start);
        Assert.Contains(writer.Reports, report => report.TargetModule == PHprModuleId.Throttle && report.State == PHprHidReportState.Start);
        Assert.Contains(result.Commands, command => command.Command.Source == PHprCommandSource.WheelLock && command.TargetModule == PHprGearPulseTarget.Brake);
        Assert.Contains(result.Commands, command => command.Command.Source == PHprCommandSource.WheelSlip && command.TargetModule == PHprGearPulseTarget.Throttle);
    }

    [Fact]
    public async Task SlipLockSimProConflictRejectsWithoutWriting()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprSlipLockRouter(
            device,
            PHprSlipLockRouterOptions.EnabledDefault,
            device.SetSafetyContext);

        var result = await router.RouteAsync(
            CreateSlipLockVehicleState(),
            RealSafetyContext() with { SoftwareConflictStatus = PHprSoftwareConflictStatus.SimProRunning });

        Assert.Equal(PHprSlipLockRoutingStatus.RejectedBySafety, result.Status);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public void RealOutputProjectDoesNotReferenceAsioAudioPath()
    {
        var referenced = typeof(SimagicPhprOutputDevice).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        Assert.DoesNotContain("HapticDrive.Asio.Audio", referenced);
        Assert.DoesNotContain("HapticDrive.Asio.Core", referenced);
        Assert.DoesNotContain("HapticDrive.Asio.App", referenced);
    }

    [Fact]
    public void DirectOutputCandidateSafeLabelDoesNotExposePrivatePath()
    {
        const string privatePath = @"\\?\hid#vid_3670&pid_0905&mi_00#8&private-serial&0&0000#{745A17A0-74D3-11D0-B6FE-00A0C90F57DA}";
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "local-hid:test",
            DevicePath = privatePath,
            DisplayName = privatePath,
            DeviceClass = "HIDClass",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = 0x3670,
            ProductId = 0x0905,
            InterfaceNumber = "00",
            CollectionNumber = "01",
            HidUsagePage = 0xFF00,
            HidUsage = 0x0001,
            OutputReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            OutputReportIds = [0],
            FeatureReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            FeatureReportIds = [PHprDirectOutputCandidate.F1EcFeatureReportId]
        }.Score();

        var selector = candidate.ToSelector();

        Assert.Equal(PHprDirectOutputCandidateConfidence.Preferred, candidate.Confidence);
        Assert.True(candidate.HasOpenableHidPath);
        Assert.False(candidate.IsRawInputOnly);
        Assert.Equal(privatePath, selector.DevicePath);
        Assert.DoesNotContain(privatePath, candidate.SafeLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("private-serial", candidate.SafeLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VID_3670/PID_0905", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("usage 0xFF00/0x0001", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("output 64 bytes", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("feature 64 bytes", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("output IDs none", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("feature IDs 0xF1", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("preferred transport FeatureReport", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("source HidDeviceInterface", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("raw-input-only False", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("openable HID path True", candidate.SafeLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void Vid3670RawInputMetadataIsFamilyButNotOpenable()
    {
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "raw-input:3670-0500",
            DevicePath = "raw-input:VID_3670&PID_0500",
            DisplayName = "Raw Input HID device",
            DeviceClass = "Raw Input HID",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.RawInputMetadata,
            VendorId = 0x3670,
            ProductId = 0x0500,
            HidUsagePage = 0x0001,
            HidUsage = 0x0004
        }.Score();

        var selector = candidate.ToSelector();

        Assert.Equal(PHprDirectOutputCandidateConfidence.SimagicFamily, candidate.Confidence);
        Assert.True(candidate.IsRawInputOnly);
        Assert.False(candidate.HasOpenableHidPath);
        Assert.False(selector.IsSelected);
        Assert.Contains("Raw Input metadata only", candidate.ConfidenceReason, StringComparison.Ordinal);
        Assert.Contains("source RawInputMetadata", candidate.SafeLabel, StringComparison.Ordinal);
        Assert.Contains("openable HID path False", candidate.SafeLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectOutputDryRunValidatesGatesWithoutWriter()
    {
        var options = ArmedOptions();

        var result = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            emergencyStopActive: false);

        Assert.True(result.CanPulse);
        Assert.Empty(result.Issues);
        Assert.True(result.OutputReportCapabilityKnown);
        Assert.False(result.FeatureReportCapabilityKnown);
        Assert.Contains("can pulse True", result.Summary, StringComparison.Ordinal);
        Assert.Contains("transport OutputReport", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectOutputDryRunBlocksOpenCheckSuccessWhenOutputReportLengthUnavailable()
    {
        var options = ArmedOptions() with
        {
            CandidateOutputReportCapabilityKnown = false,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = false,
            ReportShapeValidationFailed = true,
            ReportShapeValidationMessage = "Selected candidate HID output-report byte length is unavailable."
        };

        var result = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            emergencyStopActive: false);

        Assert.False(result.CanPulse);
        Assert.False(result.OutputReportCapabilityKnown);
        Assert.Contains("can pulse False", result.Summary, StringComparison.Ordinal);
        Assert.Contains(result.Issues, issue => issue.Contains("report shape", StringComparison.OrdinalIgnoreCase)
            || issue.Contains("output-report", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RealOutputBlocksPulseBeforeOpeningWhenOutputReportLengthUnavailable()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions() with
        {
            CandidateOutputReportCapabilityKnown = false,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = false,
            ReportShapeValidationFailed = true,
            ReportShapeValidationMessage = "Selected candidate HID output-report byte length is unavailable."
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Equal(0, writer.OpenCount);
        Assert.Empty(writer.Reports);
        Assert.Contains("report shape", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportShapeValidationUsesOutputReportCapabilitiesWithoutSendingCommand()
    {
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "hid-interface:test",
            DevicePath = @"\\?\hid#vid_3670&pid_0905#sanitized",
            DisplayName = "P-HPR HID output candidate",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = 0x3670,
            ProductId = 0x0905,
            OutputReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            OutputReportIds = [0]
        };

        var result = PHprHidReportShapeValidator.Validate(candidate, candidate.ToSelector());

        Assert.True(result.Attempted);
        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Contains("No-command", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportShapeValidationUsesFeatureReportF1EcCapabilitiesWithoutSendingCommand()
    {
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "hid-interface:feature",
            DevicePath = @"\\?\hid#vid_3670&pid_0905#sanitized",
            DisplayName = "P-HPR HID feature candidate",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = 0x3670,
            ProductId = 0x0905,
            InputReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            FeatureReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            FeatureReportIds = [PHprDirectOutputCandidate.F1EcFeatureReportId]
        }.Score();

        var selector = candidate.ToSelector();
        var result = PHprHidReportShapeValidator.Validate(candidate, selector);

        Assert.Equal(PHprHidReportTransport.FeatureReport, selector.Transport);
        Assert.Equal(PHprDirectOutputCandidate.F1EcFeatureReportId, selector.ReportId);
        Assert.Equal(SimHubF1EcRealReportEncoder.PayloadLengthBytes, selector.ReportLength);
        Assert.True(result.Attempted);
        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Equal(PHprHidReportTransport.FeatureReport, result.Transport);
        Assert.Equal(PHprDirectOutputCandidate.F1EcFeatureReportId, result.ReportId);
        Assert.Equal("F1 EC 01 01 32 0A 00", result.ExpectedFirstBytes);
        Assert.Contains("0xF1", result.Message, StringComparison.Ordinal);
        Assert.Contains("feature report ID 0xF1", candidate.SafeLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputOnlyAndFeatureOnlyReportShapesAreDistinct()
    {
        var outputCandidate = CreateCandidate(
            "output-only",
            outputLength: SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            featureLength: null,
            source: PHprDirectOutputCandidateSourceMethod.HidDeviceInterface) with
        {
            OutputReportIds = [0]
        };
        var featureCandidate = CreateCandidate(
            "feature-only",
            outputLength: null,
            featureLength: SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            source: PHprDirectOutputCandidateSourceMethod.HidDeviceInterface) with
        {
            FeatureReportIds = [PHprDirectOutputCandidate.F1EcFeatureReportId]
        };

        var outputSelector = outputCandidate.ToSelector();
        var featureSelector = featureCandidate.ToSelector();
        var outputResult = PHprHidReportShapeValidator.Validate(outputCandidate, outputSelector);
        var featureResult = PHprHidReportShapeValidator.Validate(featureCandidate, featureSelector);

        Assert.Equal(PHprHidReportTransport.OutputReport, outputSelector.Transport);
        Assert.Equal(PHprHidReportTransport.FeatureReport, featureSelector.Transport);
        Assert.Null(outputSelector.ReportId);
        Assert.Equal(PHprDirectOutputCandidate.F1EcFeatureReportId, featureSelector.ReportId);
        Assert.True(outputResult.Succeeded);
        Assert.True(featureResult.Succeeded);
    }

    [Fact]
    public void ReportShapeValidationRejectsMissingOutputReportLength()
    {
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "hid-interface:test",
            DevicePath = @"\\?\hid#vid_3670&pid_0905#sanitized",
            DisplayName = "P-HPR HID unknown output candidate",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = 0x3670,
            ProductId = 0x0905,
            InputReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes
        };

        var result = PHprHidReportShapeValidator.Validate(candidate, candidate.ToSelector());

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.True(result.Failed);
        Assert.Equal("OutputReportLengthUnavailable", result.SanitizedErrorCategory);
        Assert.Contains("open-check alone is not enough", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportShapeValidationRejectsFeatureTransportWhenFeatureCapabilityMissing()
    {
        var candidate = new PHprDirectOutputCandidate
        {
            CandidateId = "hid-interface:output",
            DevicePath = @"\\?\hid#vid_3670&pid_0905#sanitized",
            DisplayName = "P-HPR HID output candidate",
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            VendorId = 0x3670,
            ProductId = 0x0905,
            OutputReportByteLength = SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            OutputReportIds = [0]
        };

        var result = PHprHidReportShapeValidator.Validate(
            candidate,
            candidate.ToSelector(transport: PHprHidReportTransport.FeatureReport));

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Equal("FeatureReportLengthUnavailable", result.SanitizedErrorCategory);
    }

    [Fact]
    public void CandidatesWithOutputCapabilityArePreferredOverInputOnlyCandidates()
    {
        var provider = new WindowsPhprDirectOutputCandidateProvider(
            [
                new StaticCandidateProvider(
                    CreateCandidate("input-only", outputLength: null, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.HidDeviceInterface)),
                new StaticCandidateProvider(
                    CreateCandidate("output-capable", outputLength: SimHubF1EcRealReportEncoder.PayloadLengthBytes, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.HidDeviceInterface)),
                new StaticCandidateProvider(
                    CreateCandidate("raw-input", outputLength: null, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.RawInputMetadata))
            ]);

        var candidates = provider.DiscoverCandidates();

        Assert.Equal("output-capable", candidates[0].CandidateId);
        Assert.True(candidates[0].HasKnownOutputReportCapability);
        Assert.DoesNotContain(@"\\?\hid", candidates[0].SafeLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CandidatesWithFeatureCapabilityArePreferredOverInputOnlyCandidates()
    {
        var provider = new WindowsPhprDirectOutputCandidateProvider(
            [
                new StaticCandidateProvider(
                    CreateCandidate("input-only", outputLength: null, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.HidDeviceInterface)),
                new StaticCandidateProvider(
                    CreateCandidate("feature-capable", outputLength: null, featureLength: SimHubF1EcRealReportEncoder.PayloadLengthBytes, source: PHprDirectOutputCandidateSourceMethod.HidDeviceInterface) with
                    {
                        FeatureReportIds = [PHprDirectOutputCandidate.F1EcFeatureReportId]
                    }),
                new StaticCandidateProvider(
                    CreateCandidate("raw-input", outputLength: null, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.RawInputMetadata))
            ]);

        var candidates = provider.DiscoverCandidates();

        Assert.Equal("feature-capable", candidates[0].CandidateId);
        Assert.True(candidates[0].HasKnownFeatureReportCapability);
        Assert.Equal(PHprHidReportTransport.FeatureReport, candidates[0].PreferredTransport);
    }

    [Fact]
    public void RegistryMetadataSimagicFamilyPidsRemainSurfaced()
    {
        var provider = new WindowsPhprDirectOutputCandidateProvider(
            [
                new StaticCandidateProvider(
                    CreateCandidate("registry-b500", outputLength: null, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.HidRegistryMetadata) with
                    {
                        ProductId = 0xB500
                    },
                    CreateCandidate("registry-b905", outputLength: null, featureLength: null, source: PHprDirectOutputCandidateSourceMethod.HidRegistryMetadata) with
                    {
                        ProductId = 0xB905
                    })
            ]);

        var candidates = provider.DiscoverCandidates();

        Assert.Contains(candidates, candidate => candidate.ProductId == 0xB500
            && candidate.SourceMethod == PHprDirectOutputCandidateSourceMethod.HidRegistryMetadata);
        Assert.Contains(candidates, candidate => candidate.ProductId == 0xB905
            && candidate.SourceMethod == PHprDirectOutputCandidateSourceMethod.HidRegistryMetadata);
        Assert.All(candidates, candidate => Assert.False(candidate.HasOpenableHidPath));
    }

    [Fact]
    public void DirectOutputDryRunBlocksUntilSelectedTransportReportShapeIsValid()
    {
        var options = ArmedOptions() with
        {
            Selector = SelectedDevice() with
            {
                Transport = PHprHidReportTransport.FeatureReport,
                ReportId = PHprDirectOutputCandidate.F1EcFeatureReportId
            },
            CandidateOutputReportCapabilityKnown = false,
            CandidateFeatureReportCapabilityKnown = true,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = false,
            ReportShapeValidationFailed = true,
            ReportShapeValidationMessage = "Feature report shape was not validated."
        };

        var result = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            emergencyStopActive: false);

        Assert.False(result.CanPulse);
        Assert.False(result.OutputReportCapabilityKnown);
        Assert.True(result.FeatureReportCapabilityKnown);
        Assert.Contains("can pulse False", result.Summary, StringComparison.Ordinal);
        Assert.Contains("FeatureReport", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidParameterIOExceptionIsReportShapeFailure()
    {
        var exception = new IOException("invalid parameter", unchecked((int)0x80070057));

        var status = PHprHidPathSafety.ClassifyWriteExceptionStatus(exception);

        Assert.Equal(PHprHidWriteStatus.InvalidReport, status);
        Assert.Equal("IOException:0x80070057", PHprHidPathSafety.SanitizeExceptionCategory(exception));
    }

    [Fact]
    public void DirectOutputDryRunRequiresOpenCheckSuccess()
    {
        var options = ArmedOptions() with
        {
            OpenCheckAttempted = true,
            OpenCheckSucceeded = false,
            OpenCheckFailed = true,
            OpenCheckSanitizedErrorCategory = "UnauthorizedAccessException:0x80070005"
        };

        var result = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            emergencyStopActive: false);

        Assert.False(result.CanPulse);
        Assert.Contains(result.Issues, issue => issue.Contains("open-check", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RawInputOnlyCandidateCannotPassDirectOutputDryRunGates()
    {
        var options = ArmedOptions() with
        {
            CandidateSourceMethod = PHprDirectOutputCandidateSourceMethod.RawInputMetadata,
            CandidateIsRawInputOnly = true,
            CandidateHasOpenableHidPath = false
        };

        var result = PHprDirectOutputDryRunValidator.Validate(
            options,
            PHprSoftwareConflictStatus.Clear,
            emergencyStopActive: false);

        Assert.False(result.CanPulse);
        Assert.Contains(result.Issues, issue => issue.Contains("Raw Input metadata", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Contains("openable HID device-interface path", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RawInputOnlyCandidateIsRejectedBeforeOpeningWriter()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions() with
        {
            CandidateSourceMethod = PHprDirectOutputCandidateSourceMethod.RawInputMetadata,
            CandidateIsRawInputOnly = true,
            CandidateHasOpenableHidPath = false
        });

        var result = await device.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.False(result.Succeeded);
        Assert.Equal(0, writer.OpenCount);
        Assert.Empty(writer.Reports);
        Assert.Contains("openable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenCheckRejectsRawInputOnlyWithoutCreatingWriter()
    {
        var writerFactoryCalled = false;
        var runner = new PHprHidOpenCheckRunner(_ =>
        {
            writerFactoryCalled = true;
            return new FakeHidReportWriter(SelectedDevice());
        });

        var result = await runner.RunAsync(
            SelectedDevice(),
            candidateHasOpenableHidPath: false,
            candidateIsRawInputOnly: true);

        Assert.True(result.Attempted);
        Assert.True(result.Failed);
        Assert.False(result.Succeeded);
        Assert.False(writerFactoryCalled);
        Assert.Equal("RawInputOnly", result.SanitizedErrorCategory);
    }

    [Fact]
    public async Task OpenCheckOpensAndClosesWithoutWritingReports()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var runner = new PHprHidOpenCheckRunner(_ => writer);

        var result = await runner.RunAsync(
            SelectedDevice(),
            candidateHasOpenableHidPath: true,
            candidateIsRawInputOnly: false);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, writer.OpenCount);
        Assert.Equal(1, writer.CloseCount);
        Assert.Empty(writer.Reports);
        Assert.Empty(writer.WriteAttempts);
    }

    [Fact]
    public void WindowsHidPathSafetyRejectsCorruptedRelativePathBeforeOpen()
    {
        const string corruptedPath = "\uFFFD\u0001hid#vid_3670&pid_0905#private";
        Assert.False(PHprHidPathSafety.IsAbsoluteWindowsDevicePath(corruptedPath));
    }

    [Fact]
    public async Task MockPathStillRecordsMockOnlyCommand()
    {
        var mock = new MockPhprOutputDevice();

        var result = await mock.SendAsync(TestCommand(PHprModuleId.Brake));

        Assert.True(result.Succeeded);
        Assert.True(mock.CommandHistory.Single().SafetyFlags.HasFlag(PHprSafetyFlags.MockOnly));
    }

    private static PHprCommand TestCommand(PHprModuleId moduleId, int durationMs = 10)
    {
        return PHprCommand.Create(moduleId, 0.10d, 50d, durationMs, PHprCommandSource.TestBench);
    }

    [Fact]
    public void AutomatedTests_DoNotInstantiateRealUsbWriter()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testsDirectory = Path.Combine(repositoryRoot, "tests");
        var matches = Directory.GetFiles(testsDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}PHprRealOutputTests.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(match => match.line.Contains("new WindowsHidReportWriter(", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(matches);
    }

    private static PHprRealOutputOptions ArmedOptions()
    {
        return PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            DirectControlApprovalConfirmed = true,
            Selector = SelectedDevice(),
            CandidateSourceMethod = PHprDirectOutputCandidateSourceMethod.HidDeviceInterface,
            CandidateIsRawInputOnly = false,
            CandidateHasOpenableHidPath = true,
            CandidateOutputReportCapabilityKnown = true,
            CandidateFeatureReportCapabilityKnown = false,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = true,
            ReportShapeValidationFailed = false,
            ReportShapeValidationMessage = "No-command HID report-shape validation succeeded from output-report capability metadata; no P-HPR report was sent.",
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true,
            OpenCheckFailed = false,
            BrakeGearPulse = PHprRealGearPulseSettings.Default with { DurationMs = 1 },
            ThrottleGearPulse = PHprRealGearPulseSettings.Default with { DurationMs = 1 }
        };
    }

    private static PHprHidDeviceSelector SelectedDevice()
    {
        return new PHprHidDeviceSelector(
            @"\\?\hid#vid_3670&pid_0905#sanitized",
            "Sanitized P700 P-HPR HID path",
            "manual test interface",
            null,
            SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            PHprHidReportTransport.OutputReport);
    }

    private static PHprHidDeviceSelector SelectedFeatureReportDevice()
    {
        return SelectedDevice() with
        {
            ReportId = PHprDirectOutputCandidate.F1EcFeatureReportId,
            Transport = PHprHidReportTransport.FeatureReport
        };
    }

    private static PHprRealOutputOptions FeatureReportOptions()
    {
        return ArmedOptions() with
        {
            Selector = SelectedFeatureReportDevice(),
            CandidateOutputReportCapabilityKnown = false,
            CandidateFeatureReportCapabilityKnown = true,
            ReportShapeValidationMessage = "No-command HID report-shape validation succeeded from feature-report capability metadata; no P-HPR report was sent."
        };
    }

    private static PHprDirectOutputCandidate CreateCandidate(
        string id,
        int? outputLength,
        int? featureLength,
        PHprDirectOutputCandidateSourceMethod source)
    {
        return new PHprDirectOutputCandidate
        {
            CandidateId = id,
            DevicePath = source == PHprDirectOutputCandidateSourceMethod.HidDeviceInterface
                ? $@"\\?\hid#vid_3670&pid_0905#{id}"
                : $"raw-input:VID_3670&PID_0905:{id}",
            DisplayName = id,
            SourceMethod = source,
            VendorId = 0x3670,
            ProductId = 0x0905,
            OutputReportByteLength = outputLength,
            FeatureReportByteLength = featureLength,
            HidUsagePage = 0xFF00,
            HidUsage = 0x0001
        }.Score();
    }

    private static PHprSafetyContext RealSafetyContext()
    {
        return PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            RequiresRealDeviceWrites = true
        };
    }

    private static ShiftIntentEvent AcceptedShift(
        PaddleSide paddleSide = PaddleSide.Right,
        DateTimeOffset? timestampUtc = null,
        DateTimeOffset? acceptedAtUtc = null)
    {
        return ShiftIntentEvent.CreatePaddlePress(
            paddleSide,
            DrivingArmedState.Armed("test driving armed"),
            timestampUtc: timestampUtc,
            acceptedAtUtc: acceptedAtUtc);
    }

    private static ShiftIntentEvent SuppressedShift()
    {
        return ShiftIntentEvent.CreatePaddlePress(
            PaddleSide.Right,
            DrivingArmedState.NotArmed("test menu suppression"));
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

    private static VehicleState CreateSlipLockVehicleState(uint frame = 1)
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
                    SpeedKph: 120,
                    Throttle: 0.8f,
                    Steer: 0f,
                    Brake: 0.8f,
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
                    WheelSpeed: Wheels(1f),
                    WheelSlipRatio: Wheels(0.42f),
                    WheelSlipAngle: Wheels(0.12f),
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

    private static async Task WaitForWriteAttemptsAsync(FakeHidReportWriter writer, int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (writer.WriteAttempts.Count >= count)
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.True(writer.WriteAttempts.Count >= count, $"Expected {count} write attempts but saw {writer.WriteAttempts.Count}.");
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

    private sealed class FakeHidReportWriter(PHprHidDeviceSelector selector) : IPhprHidReportWriter
    {
        private readonly List<PHprHidReport> _reports = [];
        private readonly List<PHprHidReport> _writeAttempts = [];

        public PHprHidDeviceSelector Selector { get; } = selector;

        public bool IsOpen { get; private set; }

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public TimeSpan OpenDelay { get; init; }

        public TimeSpan WriteDelay { get; init; }

        public TimeSpan CloseDelay { get; init; }

        public Queue<PHprHidWriteResult> NextOpenResults { get; } = new();

        public Queue<PHprHidWriteResult> NextWriteResults { get; } = new();

        public Queue<PHprHidWriteResult> NextCloseResults { get; } = new();

        public IReadOnlyList<PHprHidReport> Reports => _reports;

        public IReadOnlyList<PHprHidReport> WriteAttempts => _writeAttempts;

        public async ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (OpenDelay > TimeSpan.Zero)
            {
                await Task.Delay(OpenDelay, cancellationToken);
            }

            OpenCount++;
            var result = NextOpenResults.Count > 0
                ? NextOpenResults.Dequeue()
                : PHprHidWriteResult.Success(Selector.ReportLength, "fake open");
            if (result.Succeeded)
            {
                IsOpen = true;
            }

            return result;
        }

        public async ValueTask<PHprHidWriteResult> WriteReportAsync(
            PHprHidReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (WriteDelay > TimeSpan.Zero)
            {
                await Task.Delay(WriteDelay, cancellationToken);
            }

            _writeAttempts.Add(report);
            var result = NextWriteResults.Count > 0
                ? NextWriteResults.Dequeue()
                : PHprHidWriteResult.Success(report.Length, "fake write");
            if (result.Succeeded)
            {
                _reports.Add(report);
            }

            return result;
        }

        public async ValueTask<PHprHidWriteResult> CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CloseDelay > TimeSpan.Zero)
            {
                await Task.Delay(CloseDelay, cancellationToken);
            }

            CloseCount++;
            var result = NextCloseResults.Count > 0
                ? NextCloseResults.Dequeue()
                : PHprHidWriteResult.Success(0, "fake close");
            if (result.Succeeded)
            {
                IsOpen = false;
            }

            return result;
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

        throw new DirectoryNotFoundException("Could not find repository root containing HapticDrive.Asio.sln.");
    }

    private sealed class StaticCandidateProvider(params PHprDirectOutputCandidate[] candidates) : IPHprDirectOutputCandidateProvider
    {
        public IReadOnlyList<PHprDirectOutputCandidate> DiscoverCandidates(DateTimeOffset? discoveredAtUtc = null)
        {
            return candidates;
        }
    }
}
