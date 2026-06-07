using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public sealed record PHprSafetySnapshot(
    PHprSafetyLimits Limits,
    PHprSafetyContext Context,
    long TotalEvaluatedCommandCount,
    long AcceptedCount,
    long AcceptedWithClampCount,
    long RejectedCount,
    long EmergencyStopCount,
    PHprSafetyDecision? LastDecision,
    PHprSafetyViolation? LastViolation,
    PHprCommand? LastAcceptedCommand,
    PHprCommand? LastRejectedCommand,
    PHprSafetyClampDetails? LastClampDetails,
    int CurrentCommandRateWindowCount,
    int CurrentContinuousDurationEstimateMs,
    bool IsEmergencyStopActive,
    bool RealWritesAllowed,
    bool RealWriteModeBlocked,
    string? LastError,
    DateTimeOffset? LastEvaluatedAtUtc);
