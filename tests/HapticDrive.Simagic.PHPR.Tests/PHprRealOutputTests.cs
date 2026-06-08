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
    public async Task StopFailureRecordsLastStopStatusWithoutClearingEmergencyLatch()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        writer.NextWriteResults.Enqueue(PHprHidWriteResult.Failure("fake stop failed", "planned stop failure"));
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());

        await device.EmergencyStopAsync();
        var diagnostics = device.GetDiagnostics();

        Assert.True(diagnostics.Output.IsEmergencyStopActive);
        Assert.Equal(PHprHidConnectionState.Faulted, diagnostics.Connection.State);
        Assert.Equal(PHprHidWriteStatus.Failed, diagnostics.Connection.LastStopStatus);
        Assert.Equal(1, diagnostics.FailedReportWriteCount);
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
    public async Task GearPaddleAcceptedEventWritesOnlyWhenEnabledAndArmed()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var device = new SimagicPhprOutputDevice(writer, ArmedOptions());
        var router = new PHprDirectGearPulseRouter(device, ArmedOptions());

        var result = await router.RouteAsync(AcceptedShift(), RealSafetyContext());

        Assert.True(result.Routed);
        Assert.Equal(2, writer.Reports.Count(report => report.State == PHprHidReportState.Start));
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
        var start = Assert.Single(writer.Reports.Where(report => report.State == PHprHidReportState.Start));
        Assert.Equal(PHprModuleId.Throttle, start.TargetModule);
        Assert.Equal(0x28, start.Payload[4]);
        Assert.Equal(0x07, start.Payload[5]);
    }

    [Fact]
    public void RealOutputProjectDoesNotReferenceAsioAudioPath()
    {
        var referenced = typeof(SimagicPhprOutputDevice).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        Assert.DoesNotContain("HapticDrive.Asio.Audio", referenced);
        Assert.DoesNotContain("HapticDrive.Asio.Core", referenced);
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

    private static PHprRealOutputOptions ArmedOptions()
    {
        return PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            Selector = SelectedDevice(),
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
            SimHubF1EcRealReportEncoder.PayloadLengthBytes);
    }

    private static PHprSafetyContext RealSafetyContext()
    {
        return PHprSafetyContext.DefaultMock with
        {
            IsMockOutput = false,
            RequiresRealDeviceWrites = true
        };
    }

    private static ShiftIntentEvent AcceptedShift()
    {
        return ShiftIntentEvent.CreatePaddlePress(
            PaddleSide.Right,
            DrivingArmedState.Armed("test driving armed"));
    }

    private static ShiftIntentEvent SuppressedShift()
    {
        return ShiftIntentEvent.CreatePaddlePress(
            PaddleSide.Right,
            DrivingArmedState.NotArmed("test menu suppression"));
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
}
