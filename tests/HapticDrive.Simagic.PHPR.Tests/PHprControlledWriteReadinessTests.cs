using HapticDrive.Simagic.PHPR.Abstractions.Readiness;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprControlledWriteReadinessTests
{
    [Fact]
    public void Stage2PDefaultReadinessDisablesAllDirectActions()
    {
        var readiness = PHprControlledWriteReadiness.Evaluate(PHprControlledWriteChecklist.Stage2PNoWriteDefault);

        Assert.True(readiness.IsNoWriteStage);
        Assert.True(readiness.IsBlocked);
        Assert.False(readiness.CanEnableDirectControl);
        Assert.False(readiness.CanArmDirectControl);
        Assert.False(readiness.CanSendManualPulse);
        Assert.Contains(readiness.Issues, issue => issue.Code == PHprControlledWriteReadinessIssueCode.StageIsNoWrite);
    }

    [Fact]
    public void FullyConfirmedFutureChecklistStillCannotSendDuringStage2P()
    {
        var checklist = new PHprControlledWriteChecklist(
            UserPhysicallyPresent: true,
            SimProClosed: true,
            SimHubClosed: true,
            P700Connected: true,
            PHprModulesInstalled: true,
            HapticDriveRunning: true,
            EmergencyStopVisible: true,
            BrakeModuleKnown: true,
            ThrottleModuleKnown: true,
            DeviceInterfaceReportSelected: true,
            RealWritesDefaultOff: true,
            DirectControlModeEnabled: true,
            DirectControlArmed: true,
            SoftwareConflictStatus: PHprSoftwareConflictStatus.Clear);

        var readiness = PHprControlledWriteReadiness.Evaluate(checklist);

        Assert.True(readiness.IsBlocked);
        Assert.False(readiness.CanSendManualPulse);
        Assert.Single(readiness.Issues);
        Assert.Equal(PHprControlledWriteReadinessIssueCode.StageIsNoWrite, readiness.Issues[0].Code);
    }

    [Fact]
    public void MissingPreconditionsAreReported()
    {
        var readiness = PHprControlledWriteReadiness.Evaluate(PHprControlledWriteChecklist.Stage2PNoWriteDefault);

        Assert.Contains(readiness.Issues, issue => issue.Code == PHprControlledWriteReadinessIssueCode.UserNotPresent);
        Assert.Contains(readiness.Issues, issue => issue.Code == PHprControlledWriteReadinessIssueCode.P700NotConfirmed);
        Assert.Contains(readiness.Issues, issue => issue.Code == PHprControlledWriteReadinessIssueCode.DeviceInterfaceReportNotSelected);
        Assert.Contains(readiness.Issues, issue => issue.Code == PHprControlledWriteReadinessIssueCode.SoftwareConflictNotClear);
    }

    [Theory]
    [InlineData(PHprSoftwareConflictStatus.Unknown)]
    [InlineData(PHprSoftwareConflictStatus.SimProRunning)]
    [InlineData(PHprSoftwareConflictStatus.SimHubRunning)]
    [InlineData(PHprSoftwareConflictStatus.ActiveConflict)]
    public void NonClearSoftwareCoexistenceBlocksReadiness(PHprSoftwareConflictStatus status)
    {
        var checklist = PHprControlledWriteChecklist.Stage2PNoWriteDefault with
        {
            SoftwareConflictStatus = status
        };

        var readiness = PHprControlledWriteReadiness.Evaluate(checklist);

        Assert.Contains(readiness.Issues, issue => issue.Code == PHprControlledWriteReadinessIssueCode.SoftwareConflictNotClear);
    }

    [Fact]
    public void TestPlanCoversRequiredSafetyItems()
    {
        var plan = PHprControlledWriteTestPlan.Stage2P;
        var allItems = string.Join(" | ", plan.Preconditions
            .Concat(plan.FirstPulseLimits)
            .Concat(plan.TestSequence)
            .Concat(plan.PassCriteria)
            .Concat(plan.AbortCriteria)
            .Concat(plan.EvidenceReferences));

        Assert.Contains("Emergency stop", allItems, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SimPro", allItems, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SimHub", allItems, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Wrong pedal", allItems, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No writes occur unless explicitly enabled and armed", allItems, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManualResultTemplateAvoidsPrivateRawCaptureFields()
    {
        var fields = string.Join(" | ", PHprManualTestResultTemplate.Stage2P.RequiredFields);

        Assert.Contains("App branch/commit", fields, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Emergency stop result", fields, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pass/fail decision", fields, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serial number", fields, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw capture", fields, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private device path", fields, StringComparison.OrdinalIgnoreCase);
    }
}
