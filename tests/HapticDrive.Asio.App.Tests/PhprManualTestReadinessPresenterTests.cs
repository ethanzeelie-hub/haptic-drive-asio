using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprManualTestReadinessPresenterTests
{
    [Fact]
    public void PhprReadinessChecklist_ShowsOpenCheckArmInterlockCoexistenceEmergencyStopWithoutPhraseStep()
    {
        var presentation = PhprManualTestReadinessPresenter.Build(new PhprManualTestReadinessSnapshot(
            CandidateSelected: true,
            OpenCheckPassed: false,
            ReportShapeValid: true,
            DirectControlEnabled: false,
            DirectControlArmed: false,
            SessionAuthorized: false,
            OutputInterlockClear: false,
            CoexistenceClear: false,
            EmergencyStopClear: false,
            DirectConnectionReadyOrOpenable: false,
            DirectConnectionState: "Closed",
            CoexistenceStatus: "BlockedBySimPro"));

        Assert.False(presentation.IsReady);
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("P-HPR candidate selected", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("HID no-write open-check passed", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Direct control enabled", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Direct control armed", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Output interlock clear", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Coexistence clear", StringComparison.Ordinal));
        Assert.Contains(presentation.ChecklistItems, item => item.Contains("Emergency stop clear", StringComparison.Ordinal));
        Assert.DoesNotContain(presentation.ChecklistItems, item => item.Contains("authorization", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("next step", presentation.DeviceStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhprPedalButtons_EnabledOnlyWhenFullChecklistSatisfied()
    {
        var presentation = PhprManualTestReadinessPresenter.Build(new PhprManualTestReadinessSnapshot(
            CandidateSelected: true,
            OpenCheckPassed: true,
            ReportShapeValid: true,
            DirectControlEnabled: true,
            DirectControlArmed: true,
            SessionAuthorized: true,
            OutputInterlockClear: true,
            CoexistenceClear: true,
            EmergencyStopClear: true,
            DirectConnectionReadyOrOpenable: true,
            DirectConnectionState: "Closed",
            CoexistenceStatus: "Clear"));

        Assert.True(presentation.IsReady);
        Assert.Contains("ready for this session", presentation.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("next step", presentation.DeviceStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.All(presentation.ChecklistItems, item => Assert.Contains(": YES.", item, StringComparison.Ordinal));
    }
}
