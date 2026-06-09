namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprHidOpenCheckResult(
    bool Attempted,
    bool Succeeded,
    bool Failed,
    PHprHidWriteStatus? OpenStatus,
    PHprHidWriteStatus? CloseStatus,
    string Message,
    string? SanitizedErrorCategory,
    DateTimeOffset? AttemptedAtUtc)
{
    public static PHprHidOpenCheckResult NotAttempted { get; } = new(
        Attempted: false,
        Succeeded: false,
        Failed: false,
        OpenStatus: null,
        CloseStatus: null,
        "Open-check has not been attempted.",
        SanitizedErrorCategory: null,
        AttemptedAtUtc: null);

    public static PHprHidOpenCheckResult Success(PHprHidWriteStatus? closeStatus = PHprHidWriteStatus.Succeeded)
    {
        return new PHprHidOpenCheckResult(
            Attempted: true,
            Succeeded: true,
            Failed: false,
            OpenStatus: PHprHidWriteStatus.Succeeded,
            CloseStatus: closeStatus,
            "P-HPR HID open-check succeeded; writer was opened and closed without sending a report.",
            SanitizedErrorCategory: null,
            AttemptedAtUtc: DateTimeOffset.UtcNow);
    }

    public static PHprHidOpenCheckResult Failure(
        string message,
        PHprHidWriteStatus? openStatus,
        string? sanitizedErrorCategory)
    {
        return new PHprHidOpenCheckResult(
            Attempted: true,
            Succeeded: false,
            Failed: true,
            OpenStatus: openStatus,
            CloseStatus: null,
            message,
            sanitizedErrorCategory,
            AttemptedAtUtc: DateTimeOffset.UtcNow);
    }
}
