namespace HapticDrive.Simagic.PHPR.Output.Windows;

public enum PHprHidWriteStatus
{
    Succeeded = 0,
    Failed = 1,
    NotSelected = 2,
    InvalidReport = 3,
    TimedOut = 4,
    Disconnected = 5,
    Cancelled = 6
}
