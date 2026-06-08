using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Readiness;

public sealed record PHprControlledWriteReadiness(
    string Status,
    bool IsNoWriteStage,
    bool CanEnableDirectControl,
    bool CanArmDirectControl,
    bool CanSendManualPulse,
    IReadOnlyList<PHprControlledWriteReadinessIssue> Issues)
{
    public bool IsBlocked => IsNoWriteStage || Issues.Any(issue => issue.IsBlocking);

    public static PHprControlledWriteReadiness Evaluate(PHprControlledWriteChecklist checklist)
    {
        ArgumentNullException.ThrowIfNull(checklist);

        var issues = new List<PHprControlledWriteReadinessIssue>
        {
            new(
                PHprControlledWriteReadinessIssueCode.StageIsNoWrite,
                "Stage 2P is documentation/readiness only; no real P-HPR write adapter or write-capable UI exists.")
        };

        AddIf(issues, !checklist.DirectControlModeEnabled, PHprControlledWriteReadinessIssueCode.DirectControlDisabled, "Direct-control mode is disabled.");
        AddIf(issues, !checklist.DirectControlArmed, PHprControlledWriteReadinessIssueCode.DirectControlNotArmed, "Direct control is not armed, and arming must never be persisted.");
        AddIf(issues, !checklist.UserPhysicallyPresent, PHprControlledWriteReadinessIssueCode.UserNotPresent, "A physically present user has not confirmed manual test supervision.");
        AddIf(issues, !checklist.SimProClosed, PHprControlledWriteReadinessIssueCode.SimProNotClosed, "SimPro Manager is not confirmed closed for the first controlled write test.");
        AddIf(issues, !checklist.SimHubClosed, PHprControlledWriteReadinessIssueCode.SimHubNotClosed, "SimHub is not confirmed closed for the first controlled write test.");
        AddIf(issues, !checklist.P700Connected, PHprControlledWriteReadinessIssueCode.P700NotConfirmed, "P700 connection is not confirmed for direct control.");
        AddIf(issues, !checklist.PHprModulesInstalled, PHprControlledWriteReadinessIssueCode.PHprModulesNotConfirmed, "Brake and throttle P-HPR module installation is not confirmed.");
        AddIf(issues, !checklist.EmergencyStopVisible, PHprControlledWriteReadinessIssueCode.EmergencyStopNotVisible, "Emergency stop is not confirmed visible.");
        AddIf(issues, !checklist.BrakeModuleKnown, PHprControlledWriteReadinessIssueCode.BrakeModuleUnknown, "Brake P-HPR module mapping is not confirmed.");
        AddIf(issues, !checklist.ThrottleModuleKnown, PHprControlledWriteReadinessIssueCode.ThrottleModuleUnknown, "Throttle P-HPR module mapping is not confirmed.");
        AddIf(issues, !checklist.DeviceInterfaceReportSelected, PHprControlledWriteReadinessIssueCode.DeviceInterfaceReportNotSelected, "No device/interface/report selection is confirmed.");
        AddIf(issues, !checklist.RealWritesDefaultOff, PHprControlledWriteReadinessIssueCode.RealWritesNotDefaultOff, "Real writes must default off.");

        if (checklist.SoftwareConflictStatus != PHprSoftwareConflictStatus.Clear)
        {
            issues.Add(new PHprControlledWriteReadinessIssue(
                PHprControlledWriteReadinessIssueCode.SoftwareConflictNotClear,
                $"SimPro/SimHub coexistence status is {checklist.SoftwareConflictStatus}; first direct-control tests require Clear."));
        }

        return new PHprControlledWriteReadiness(
            "Stage 2P no-write readiness: direct P-HPR control remains disabled.",
            IsNoWriteStage: true,
            CanEnableDirectControl: false,
            CanArmDirectControl: false,
            CanSendManualPulse: false,
            issues);
    }

    private static void AddIf(
        ICollection<PHprControlledWriteReadinessIssue> issues,
        bool condition,
        PHprControlledWriteReadinessIssueCode code,
        string message)
    {
        if (condition)
        {
            issues.Add(new PHprControlledWriteReadinessIssue(code, message));
        }
    }
}
