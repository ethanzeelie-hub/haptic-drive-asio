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

        Assert.Equal(2, writer.Reports.Count);
        Assert.All(writer.Reports, report => Assert.Equal(PHprHidReportState.EmergencyStop, report.State));
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

        public PHprHidDeviceSelector Selector { get; } = selector;

        public IReadOnlyList<PHprHidReport> Reports => _reports;

        public ValueTask<PHprHidWriteResult> WriteReportAsync(
            PHprHidReport report,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reports.Add(report);
            return ValueTask.FromResult(PHprHidWriteResult.Success(report.Length, "fake write"));
        }
    }
}
