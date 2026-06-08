namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprHidWriteResult(
    bool Succeeded,
    string Message,
    int ReportLength,
    DateTimeOffset CompletedAtUtc,
    string? ErrorMessage = null,
    PHprHidWriteStatus Status = PHprHidWriteStatus.Succeeded)
{
    public static PHprHidWriteResult Success(int reportLength, string message)
    {
        return new PHprHidWriteResult(true, message, reportLength, DateTimeOffset.UtcNow, Status: PHprHidWriteStatus.Succeeded);
    }

    public static PHprHidWriteResult Failure(
        string message,
        string? errorMessage = null,
        PHprHidWriteStatus status = PHprHidWriteStatus.Failed)
    {
        return new PHprHidWriteResult(false, message, 0, DateTimeOffset.UtcNow, errorMessage, status);
    }
}
