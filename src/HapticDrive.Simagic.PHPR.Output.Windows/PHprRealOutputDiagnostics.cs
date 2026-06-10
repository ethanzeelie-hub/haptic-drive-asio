using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprRealOutputDiagnostics(
    PHprRealOutputOptions Options,
    PHprDirectControlArmingState Arming,
    PHprOutputSnapshot Output,
    PHprRealOutputConnectionDiagnostics Connection,
    long ReportWriteCount,
    long FailedReportWriteCount,
    int LastReportLength,
    PHprModuleId? LastTarget,
    PHprHidReportState? LastReportState,
    string? LastReportSummary,
    string? LastError,
    bool ActivePulse,
    DateTimeOffset? LastStartSentAtUtc,
    DateTimeOffset? LastStopSentAtUtc,
    PHprModuleId? LastStartReportTarget,
    PHprModuleId? LastStopReportTarget,
    PHprHidWriteStatus? LastStopResultStatus,
    string? LastStopResultMessage,
    int? LastScheduledPulseDurationMs);
