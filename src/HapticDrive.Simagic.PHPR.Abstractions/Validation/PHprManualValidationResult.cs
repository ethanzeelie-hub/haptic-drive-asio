namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public sealed record PHprManualValidationResult(
    DateTimeOffset CreatedAtUtc,
    string AppBranchOrCommit,
    bool P700Connected,
    bool BrakeModuleInstalled,
    bool ThrottleModuleInstalled,
    string P700DeviceInfo,
    string SimProStatus,
    string SimHubStatus,
    string SelectedDeviceInterfaceReport,
    string BrakeTestResult,
    string ThrottleTestResult,
    string EmergencyStopResult,
    string PaddleUpshiftResult,
    string PaddleDownshiftResult,
    string WrongPedalBehavior,
    string SustainedVibrationBehavior,
    string Notes,
    string PassFailDecision)
{
    public bool PassRequested => PassFailDecision.Trim().Equals("pass", StringComparison.OrdinalIgnoreCase);

    public PHprManualValidationResultEvaluation Evaluate()
    {
        var issues = new List<PHprManualValidationIssue>();
        AddMissing(issues, nameof(AppBranchOrCommit), AppBranchOrCommit);
        AddMissing(issues, nameof(P700DeviceInfo), P700DeviceInfo);
        AddMissing(issues, nameof(SimProStatus), SimProStatus);
        AddMissing(issues, nameof(SimHubStatus), SimHubStatus);
        AddMissing(issues, nameof(SelectedDeviceInterfaceReport), SelectedDeviceInterfaceReport);
        AddMissing(issues, nameof(BrakeTestResult), BrakeTestResult);
        AddMissing(issues, nameof(ThrottleTestResult), ThrottleTestResult);
        AddMissing(issues, nameof(EmergencyStopResult), EmergencyStopResult);
        AddMissing(issues, nameof(PaddleUpshiftResult), PaddleUpshiftResult);
        AddMissing(issues, nameof(PaddleDownshiftResult), PaddleDownshiftResult);
        AddMissing(issues, nameof(WrongPedalBehavior), WrongPedalBehavior);
        AddMissing(issues, nameof(SustainedVibrationBehavior), SustainedVibrationBehavior);
        AddMissing(issues, nameof(PassFailDecision), PassFailDecision);

        AddIf(issues, !P700Connected, "P700 connection must be confirmed before a pass decision.");
        AddIf(issues, !BrakeModuleInstalled, "Brake P-HPR module installation must be confirmed before a pass decision.");
        AddIf(issues, !ThrottleModuleInstalled, "Throttle P-HPR module installation must be confirmed before a pass decision.");

        var canMarkPass = PassRequested && issues.Count == 0;
        return new PHprManualValidationResultEvaluation(
            PassRequested,
            canMarkPass,
            string.IsNullOrWhiteSpace(PassFailDecision),
            issues);
    }

    private static void AddMissing(
        ICollection<PHprManualValidationIssue> issues,
        string fieldName,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new PHprManualValidationIssue(
                PHprManualValidationIssueCode.MissingRequiredResultField,
                $"{fieldName} is required before marking validation as pass."));
        }
    }

    private static void AddIf(
        ICollection<PHprManualValidationIssue> issues,
        bool condition,
        string message)
    {
        if (condition)
        {
            issues.Add(new PHprManualValidationIssue(
                PHprManualValidationIssueCode.RequiredHardwareFlagNotConfirmed,
                message));
        }
    }
}
