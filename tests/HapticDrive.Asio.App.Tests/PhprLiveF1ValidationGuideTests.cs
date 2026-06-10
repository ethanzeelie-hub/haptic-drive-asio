using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprLiveF1ValidationGuideTests
{
    [Fact]
    public void GuideIncludesAllManualLiveF1StepsWithoutClaimingPhysicalValidation()
    {
        var status = PhprLiveF1ValidationGuide.Build(BaseSnapshot());

        Assert.Equal(12, status.Checklist.Count);
        Assert.Contains(status.Checklist, item => item.Contains("App open, direct control disabled", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("F1 25 telemetry active", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Paddle press accepted", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Mock mode gear pulse diagnostics", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Real mode direct ready", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Brake/throttle gear pulse test", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Road vibration test", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Slip/lock test if safe", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Menu/tabbing suppression", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("Emergency stop", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("SimPro/SimHub conflict warning", StringComparison.Ordinal));
        Assert.Contains("physical validation pending local Ethan run", status.DiagnosticsLine, StringComparison.Ordinal);
        Assert.DoesNotContain("validated", status.DiagnosticsLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuideReportsLiveTelemetryAndReadyMockValidationGates()
    {
        var status = PhprLiveF1ValidationGuide.Build(BaseSnapshot() with
        {
            PipelineRunning = true,
            UdpReceiverRunning = true,
            UdpPacketCount = 42,
            ParserSuccessCount = 42,
            TelemetryAge = TimeSpan.FromMilliseconds(18),
            DrivingArmed = true,
            DrivingArmedReason = "Active driving telemetry is fresh.",
            PaddleListenerStatus = "Running",
            ShiftIntentEnabled = true,
            AcceptedShiftIntentCount = 2,
            OutputMode = "Mock",
            MockGearRoutingEnabled = true,
            CoexistenceStatus = "Clear"
        });

        Assert.Contains("ready for supervised live mock validation", status.Summary, StringComparison.Ordinal);
        Assert.Contains("active from live UDP", status.Summary, StringComparison.Ordinal);
        Assert.Contains("DrivingArmed true", status.DiagnosticsLine, StringComparison.Ordinal);
        Assert.Contains("accepted 2", status.DiagnosticsLine, StringComparison.Ordinal);
        Assert.Contains("coexistence Clear", status.DiagnosticsLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GuideReportsRealModeAsManualAndSessionScoped()
    {
        var status = PhprLiveF1ValidationGuide.Build(BaseSnapshot() with
        {
            OutputMode = "Real Direct Control",
            DirectControlEnabled = true,
            DirectControlArmed = true,
            SelectedOutputConfigured = true,
            RealRoadVibrationEnabled = true,
            RealSlipLockEnabled = true
        });

        Assert.Contains(status.Checklist, item => item.Contains("direct control enabled", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("selected output selected for this session", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("local supervision", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("real road vibration enabled", StringComparison.Ordinal));
        Assert.Contains(status.Checklist, item => item.Contains("real slip/lock enabled", StringComparison.Ordinal));
        Assert.DoesNotContain("DevicePath", status.DiagnosticsLine, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serial", status.DiagnosticsLine, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\\?\hid", status.DiagnosticsLine, StringComparison.OrdinalIgnoreCase);
    }

    private static PhprLiveF1ValidationSnapshot BaseSnapshot()
    {
        return new PhprLiveF1ValidationSnapshot(
            TelemetryInputSource: "LiveUdp",
            PipelineRunning: false,
            UdpReceiverRunning: false,
            UdpPacketCount: 0,
            ParserSuccessCount: 0,
            TelemetryAge: null,
            TelemetryTimedOutMuted: false,
            DrivingArmed: false,
            DrivingArmedReason: "No recent valid telemetry has been observed.",
            PaddleListenerStatus: "NotConfigured",
            ShiftIntentEnabled: false,
            AcceptedShiftIntentCount: 0,
            SuppressedShiftIntentCount: 0,
            OutputMode: "Disabled",
            MockGearRoutingEnabled: false,
            DirectControlEnabled: false,
            DirectControlArmed: false,
            SelectedOutputConfigured: false,
            CoexistenceStatus: "Unknown",
            EmergencyStopActive: false,
            RealRoadVibrationEnabled: false,
            RealSlipLockEnabled: false);
    }
}
