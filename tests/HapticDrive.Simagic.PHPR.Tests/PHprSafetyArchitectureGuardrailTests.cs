using System.Text.Json;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprSafetyArchitectureGuardrailTests
{
    [Fact]
    public async Task PhysicalWriteBoundary_RequiresSessionAuthorization()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var interlock = ReadyInterlock();
        var device = new SimagicPhprOutputDevice(
            writer,
            ReadyOptions(),
            interlock,
            new PHprSessionWriteAuthorization());

        var result = await device.SendAsync(TestCommand());

        Assert.False(result.Succeeded);
        Assert.Contains("session authorization", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, writer.OpenCount);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task PhysicalWriteBoundary_RequiresClearInterlock()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var interlock = new OutputInterlock();
        var authorization = new PHprSessionWriteAuthorization();
        Assert.True(authorization.TryAuthorize(PHprControlledWriteApproval.Phrase));
        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "latched for test");
        var device = new SimagicPhprOutputDevice(
            writer,
            ReadyOptions(),
            interlock,
            authorization);

        var result = await device.SendAsync(TestCommand());

        Assert.False(result.Succeeded);
        Assert.Contains("interlock", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, writer.OpenCount);
        Assert.Empty(writer.Reports);
    }

    [Fact]
    public async Task StopCleanup_RemainsAllowedWhileUnauthorizedAndLatched()
    {
        var writer = new FakeHidReportWriter(SelectedDevice());
        var interlock = new OutputInterlock();
        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "latched for stop test");
        var device = new SimagicPhprOutputDevice(
            writer,
            ReadyOptions(),
            interlock,
            new PHprSessionWriteAuthorization());

        var stopAll = await device.StopAllAsync();
        await device.EmergencyStopAsync();

        Assert.True(stopAll.Succeeded, stopAll.Message);
        Assert.True(writer.Reports.Count >= 2);
        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.Stop);
        Assert.Contains(writer.Reports, report => report.State == PHprHidReportState.EmergencyStop);
    }

    [Fact]
    public void AuthorizationSnapshot_DoesNotSerializeApprovalPhrase()
    {
        var authorization = new PHprSessionWriteAuthorization();
        Assert.True(authorization.TryAuthorize(PHprControlledWriteApproval.Phrase));

        var json = JsonSerializer.Serialize(authorization.Current);

        Assert.DoesNotContain(PHprControlledWriteApproval.Phrase, json, StringComparison.Ordinal);
        Assert.Contains("Authorized for this session", json, StringComparison.Ordinal);
    }

    private static OutputInterlock ReadyInterlock()
    {
        var interlock = new OutputInterlock();
        Assert.True(interlock.Reset("clear for test"));
        return interlock;
    }

    private static PHprHidDeviceSelector SelectedDevice()
    {
        return new PHprHidDeviceSelector(
            @"\\?\hid#vid_3670&pid_0905#guardrail",
            "Guardrail device",
            "guardrail interface",
            0xF1,
            SimHubF1EcRealReportEncoder.PayloadLengthBytes,
            PHprHidReportTransport.OutputReport);
    }

    private static PHprRealOutputOptions ReadyOptions()
    {
        return PHprRealOutputOptions.Disabled with
        {
            DirectControlEnabled = true,
            DirectControlArmed = true,
            Selector = SelectedDevice(),
            CandidateHasOpenableHidPath = true,
            CandidateOutputReportCapabilityKnown = true,
            ReportShapeValidationAttempted = true,
            ReportShapeValidationSucceeded = true,
            OpenCheckAttempted = true,
            OpenCheckSucceeded = true
        };
    }

    private static PHprCommand TestCommand()
    {
        return PHprCommand.Create(
            PHprModuleId.Brake,
            strength01: 0.5d,
            frequencyHz: 50d,
            durationMs: 30,
            source: PHprCommandSource.TestBench,
            priority: 100,
            timestampUtc: new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero));
    }

    private sealed class FakeHidReportWriter(PHprHidDeviceSelector selector) : IPhprHidReportWriter
    {
        private readonly List<PHprHidReport> _reports = [];

        public PHprHidDeviceSelector Selector { get; } = selector;

        public bool IsOpen { get; private set; }

        public int OpenCount { get; private set; }

        public IReadOnlyList<PHprHidReport> Reports => _reports;

        public ValueTask<PHprHidWriteResult> OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            IsOpen = true;
            return ValueTask.FromResult(PHprHidWriteResult.Success(Selector.ReportLength, "fake open"));
        }

        public ValueTask<PHprHidWriteResult> WriteReportAsync(PHprHidReport report, CancellationToken cancellationToken = default)
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
}
