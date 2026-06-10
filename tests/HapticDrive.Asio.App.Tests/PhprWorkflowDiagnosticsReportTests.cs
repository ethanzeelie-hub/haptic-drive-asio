using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprWorkflowDiagnosticsReportTests
{
    [Fact]
    public void WorkflowReportLineSummarizesModesWithoutPrivateDeviceData()
    {
        var line = PhprWorkflowDiagnosticsReport.BuildWorkflowLine(new PhprWorkflowDiagnosticsSnapshot(
            "Real Direct Control",
            "Replay",
            "test-session.hdrec",
            ReplayPacketsReplayed: 5,
            RealDirectControlEnabled: true,
            RealDirectControlArmed: true,
            SelectedOutputIsConfigured: true,
            MockGearRoutingEnabled: false,
            MockPedalEffectsEnabled: true,
            RealRoadVibrationEnabled: true,
            RealSlipLockEnabled: false));

        Assert.Contains("P-HPR workflow: mode Real Direct Control", line, StringComparison.Ordinal);
        Assert.Contains("telemetry input Replay", line, StringComparison.Ordinal);
        Assert.Contains("replay source test-session.hdrec", line, StringComparison.Ordinal);
        Assert.Contains("replay packets 5", line, StringComparison.Ordinal);
        Assert.Contains("real direct enabled", line, StringComparison.Ordinal);
        Assert.Contains("selected output True", line, StringComparison.Ordinal);
        Assert.Contains("mock pedal effects enabled", line, StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serial", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\\?\hid", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfilePersistenceLineDocumentsSafeBoundary()
    {
        var line = PhprWorkflowDiagnosticsReport.BuildProfilePersistenceLine(
            @"C:\local\default.hdprofile.json",
            @"C:\local\p-hpr.hdphprprofile.json");

        Assert.Contains("audio", line, StringComparison.Ordinal);
        Assert.Contains("P-HPR", line, StringComparison.Ordinal);
        Assert.Contains("effect preferences only", line, StringComparison.Ordinal);
        Assert.Contains("excludes direct-enable/device/emergency state", line, StringComparison.Ordinal);
    }
}
