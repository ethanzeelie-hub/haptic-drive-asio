namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprRealOutputConnectionDiagnostics(
    PHprHidConnectionState State,
    bool WriterOpen,
    long OpenAttemptCount,
    long OpenSuccessCount,
    long CloseAttemptCount,
    long CloseSuccessCount,
    long StopReportWriteCount,
    long TimeoutCount,
    long DisconnectCount,
    long InvalidReportCount,
    PHprHidWriteStatus? LastOpenStatus,
    PHprHidWriteStatus? LastWriteStatus,
    PHprHidWriteStatus? LastStopStatus,
    PHprHidWriteStatus? LastCloseStatus,
    DateTimeOffset? LastOpenAtUtc,
    DateTimeOffset? LastWriteAtUtc,
    DateTimeOffset? LastStopAtUtc,
    DateTimeOffset? LastCloseAtUtc)
{
    public static PHprRealOutputConnectionDiagnostics Closed { get; } = new(
        PHprHidConnectionState.Closed,
        WriterOpen: false,
        OpenAttemptCount: 0,
        OpenSuccessCount: 0,
        CloseAttemptCount: 0,
        CloseSuccessCount: 0,
        StopReportWriteCount: 0,
        TimeoutCount: 0,
        DisconnectCount: 0,
        InvalidReportCount: 0,
        LastOpenStatus: null,
        LastWriteStatus: null,
        LastStopStatus: null,
        LastCloseStatus: null,
        LastOpenAtUtc: null,
        LastWriteAtUtc: null,
        LastStopAtUtc: null,
        LastCloseAtUtc: null);
}
