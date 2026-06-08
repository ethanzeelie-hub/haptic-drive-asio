namespace HapticDrive.Simagic.PHPR.Abstractions.Readiness;

public sealed record PHprControlledWriteReadinessIssue(
    PHprControlledWriteReadinessIssueCode Code,
    string Message,
    bool IsBlocking = true);
