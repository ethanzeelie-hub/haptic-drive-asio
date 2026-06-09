using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;
using HapticDrive.Simagic.PHPR.Research;
using HapticDrive.Simagic.PHPR.Research.ControlledWrite;

namespace HapticDrive.Simagic.PHPR.Research.Tests;

public sealed class SimagicControlledWriteCliTests
{
    [Fact]
    public async Task CliHelp_ListsControlledWriteCommand()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("controlled-write-test", output.ToString(), StringComparison.Ordinal);
        Assert.Contains(ControlledPhprWriteTestOptions.ApprovalPhrase, output.ToString(), StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task Cli_ControlledWriteDryRunDoesNotPrintPrivateDevicePath()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        const string privatePath = @"\\?\hid#vid_3670&pid_0905#private";

        var exitCode = await SimagicResearchCli.RunAsync(
            [
                "controlled-write-test",
                "--approval",
                ControlledPhprWriteTestOptions.ApprovalPhrase,
                "--device-path",
                privatePath,
                "--target",
                "brake"
            ],
            output,
            error);

        var text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("dry run", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Selected device path: configured for this run", text, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePath, text, StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task Cli_ControlledWriteExecuteWithoutApprovalIsBlocked()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        const string privatePath = @"\\?\hid#vid_3670&pid_0905#private";

        var exitCode = await SimagicResearchCli.RunAsync(
            ["controlled-write-test", "--execute", "--device-path", privatePath],
            output,
            error);

        var text = output.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("blocked", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exact approval phrase required", text, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePath, text, StringComparison.Ordinal);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task Runner_DryRunDoesNotCreateOrOpenWriter()
    {
        var writerFactoryCalled = false;
        var runner = new ControlledPhprWriteTestRunner(
            _ =>
            {
                writerFactoryCalled = true;
                return new FakeHidReportWriter();
            },
            CreateClearCoexistenceSnapshot,
            (_, _) => Task.CompletedTask);

        var result = await runner.RunAsync(new ControlledPhprWriteTestOptions
        {
            Execute = false,
            ApprovalPhraseText = ControlledPhprWriteTestOptions.ApprovalPhrase,
            DevicePath = @"\\?\hid#vid_3670&pid_0905#private"
        });

        Assert.True(result.Succeeded);
        Assert.False(result.Executed);
        Assert.False(writerFactoryCalled);
        Assert.Null(result.Diagnostics);
    }

    [Fact]
    public async Task Cli_DirectOutputDryRunDoesNotPrintPrivatePaths()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await SimagicResearchCli.RunAsync(["direct-output-dry-run"], output, error);

        var text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("no HID writer is opened", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\\?\", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#private", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task Runner_ExecutesFakeBrakeThrottleSequenceAndEmergencyStop()
    {
        var writer = new FakeHidReportWriter();
        var runner = new ControlledPhprWriteTestRunner(
            selector =>
            {
                writer.Configure(selector);
                return writer;
            },
            CreateClearCoexistenceSnapshot,
            (_, _) => Task.CompletedTask);

        var result = await runner.RunAsync(new ControlledPhprWriteTestOptions
        {
            Execute = true,
            ApprovalPhraseText = ControlledPhprWriteTestOptions.ApprovalPhrase,
            DevicePath = @"\\?\hid#vid_3670&pid_0905#private",
            Target = ControlledPhprWriteTarget.Sequence,
            StrengthPercent = 10d,
            FrequencyHz = 50d,
            DurationMs = 10
        });

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Executed);
        Assert.Contains(writer.Reports, report => report.TargetModule == HapticDrive.Simagic.PHPR.Abstractions.Commands.PHprModuleId.Brake
            && report.State == PHprHidReportState.Start);
        Assert.Contains(writer.Reports, report => report.TargetModule == HapticDrive.Simagic.PHPR.Abstractions.Commands.PHprModuleId.Throttle
            && report.State == PHprHidReportState.Start);
        Assert.Contains(writer.Reports, report => report.TargetModule == HapticDrive.Simagic.PHPR.Abstractions.Commands.PHprModuleId.Brake
            && report.State == PHprHidReportState.EmergencyStop);
        Assert.Contains(writer.Reports, report => report.TargetModule == HapticDrive.Simagic.PHPR.Abstractions.Commands.PHprModuleId.Throttle
            && report.State == PHprHidReportState.EmergencyStop);
        Assert.True(writer.OpenCount >= 1);
        Assert.True(writer.CloseCount >= 1);
    }

    private static PHprSoftwareCoexistenceSnapshot CreateClearCoexistenceSnapshot()
    {
        return new PHprSoftwareCoexistenceSnapshot(
            PHprSoftwareConflictStatus.Clear,
            SimProRunning: false,
            SimHubRunning: false,
            [],
            [],
            DateTimeOffset.UtcNow,
            IsSupported: true,
            "No SimPro Manager or SimHub process was detected.");
    }

    private sealed class FakeHidReportWriter : IPhprHidReportWriter
    {
        private readonly List<PHprHidReport> _reports = [];
        private PHprHidDeviceSelector _selector = PHprHidDeviceSelector.None;

        public PHprHidDeviceSelector Selector => _selector;

        public bool IsOpen { get; private set; }

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public IReadOnlyList<PHprHidReport> Reports => _reports;

        public void Configure(PHprHidDeviceSelector selector)
        {
            _selector = selector;
        }

        public ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            IsOpen = true;
            return ValueTask.FromResult(PHprHidWriteResult.Success(_selector.ReportLength, "fake open"));
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
            CloseCount++;
            IsOpen = false;
            return ValueTask.FromResult(PHprHidWriteResult.Success(0, "fake close"));
        }
    }
}
