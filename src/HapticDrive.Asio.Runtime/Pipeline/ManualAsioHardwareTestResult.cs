namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record ManualAsioHardwareTestResult(
    bool Succeeded,
    string Message,
    ManualAsioHardwareTestSnapshot Snapshot)
{
    public static ManualAsioHardwareTestResult Success(
        string message,
        ManualAsioHardwareTestSnapshot snapshot)
    {
        return new ManualAsioHardwareTestResult(true, message, snapshot);
    }

    public static ManualAsioHardwareTestResult Blocked(
        string message,
        ManualAsioHardwareTestSnapshot snapshot)
    {
        return new ManualAsioHardwareTestResult(false, message, snapshot);
    }
}
