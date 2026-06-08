namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public sealed record PHprManualValidationIssue(
    PHprManualValidationIssueCode Code,
    string Message,
    bool IsBlocking = true);
