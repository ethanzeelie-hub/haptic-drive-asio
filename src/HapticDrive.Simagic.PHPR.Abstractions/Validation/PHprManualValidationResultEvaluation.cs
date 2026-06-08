namespace HapticDrive.Simagic.PHPR.Abstractions.Validation;

public sealed record PHprManualValidationResultEvaluation(
    bool PassRequested,
    bool CanMarkPass,
    bool IsDraft,
    IReadOnlyList<PHprManualValidationIssue> Issues)
{
    public bool IsBlockedPass => PassRequested && !CanMarkPass;
}
