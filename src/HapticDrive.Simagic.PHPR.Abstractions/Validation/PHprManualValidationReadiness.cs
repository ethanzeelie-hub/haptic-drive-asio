using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public sealed record PHprManualValidationReadiness(
    string Status,
    bool CanRunBrakePulse,
    bool CanRunThrottlePulse,
    bool CanRunGearPaddleTest,
    IReadOnlyList<PHprManualValidationIssue> Issues)
{
    public bool IsBlocked => Issues.Any(issue => issue.IsBlocking);

    public static PHprManualValidationReadiness Evaluate(PHprManualValidationChecklist checklist)
    {
        ArgumentNullException.ThrowIfNull(checklist);

        var issues = new List<PHprManualValidationIssue>();
        AddIf(issues, !checklist.UserPhysicallyPresent, PHprManualValidationIssueCode.UserNotPresent, "User has not confirmed physical supervision.");
        AddIf(issues, !checklist.P700Connected, PHprManualValidationIssueCode.P700NotConnected, "P700 connection is not confirmed.");
        AddIf(issues, !checklist.BrakeModuleInstalled, PHprManualValidationIssueCode.BrakeModuleNotInstalled, "Brake P-HPR module installation is not confirmed.");
        AddIf(issues, !checklist.ThrottleModuleInstalled, PHprManualValidationIssueCode.ThrottleModuleNotInstalled, "Throttle P-HPR module installation is not confirmed.");
        AddIf(issues, !checklist.DirectControlEnabled, PHprManualValidationIssueCode.DirectControlDisabled, "Direct control is disabled.");
        AddIf(issues, !checklist.DirectControlArmed, PHprManualValidationIssueCode.DirectControlNotArmed, "Direct control is not armed.");
        AddIf(issues, !checklist.DeviceInterfaceReportSelected, PHprManualValidationIssueCode.DeviceInterfaceReportNotSelected, "Device/interface/report selection is not confirmed.");
        AddIf(issues, !checklist.SafetyLimitsVisible, PHprManualValidationIssueCode.SafetyLimitsNotVisible, "Safety limits are not visible.");
        AddIf(issues, !checklist.EmergencyStopVisible, PHprManualValidationIssueCode.EmergencyStopNotVisible, "Emergency stop is not visible.");
        AddIf(issues, !checklist.EmergencyStopClear, PHprManualValidationIssueCode.EmergencyStopLatched, "Emergency stop latch is active.");

        if (checklist.SoftwareConflictStatus != PHprSoftwareConflictStatus.Clear)
        {
            issues.Add(new PHprManualValidationIssue(
                PHprManualValidationIssueCode.SoftwareConflictNotClear,
                $"SimPro/SimHub coexistence status is {checklist.SoftwareConflictStatus}; manual validation requires Clear."));
        }

        var baseReady = issues.Count == 0;
        var brakeReady = baseReady && checklist.BrakeTestPulseAvailable;
        var throttleReady = baseReady && checklist.ThrottleTestPulseAvailable;
        var gearReady = baseReady && checklist.GearPaddleTestPlanned;
        AddIf(issues, baseReady && !checklist.BrakeTestPulseAvailable, PHprManualValidationIssueCode.BrakePulseUnavailable, "Brake pulse button is not available.");
        AddIf(issues, baseReady && !checklist.ThrottleTestPulseAvailable, PHprManualValidationIssueCode.ThrottlePulseUnavailable, "Throttle pulse button is not available.");
        AddIf(issues, baseReady && !checklist.GearPaddleTestPlanned, PHprManualValidationIssueCode.GearPaddleTestNotPlanned, "Gear paddle test is not marked planned.");

        return new PHprManualValidationReadiness(
            issues.Count == 0
                ? "Manual validation harness ready; physical test execution remains user-supervised only."
                : "Manual validation harness blocked until checklist issues are resolved.",
            brakeReady,
            throttleReady,
            gearReady,
            issues);
    }

    private static void AddIf(
        ICollection<PHprManualValidationIssue> issues,
        bool condition,
        PHprManualValidationIssueCode code,
        string message)
    {
        if (condition)
        {
            issues.Add(new PHprManualValidationIssue(code, message));
        }
    }
}
