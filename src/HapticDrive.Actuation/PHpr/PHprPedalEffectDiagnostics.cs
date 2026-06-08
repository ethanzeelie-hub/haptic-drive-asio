using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprPedalEffectDiagnostics(
    PHprPedalEffectKind Kind,
    PHprPedalEffectState State,
    bool IsActive,
    double Intensity01,
    PHprGearPulseTarget? LastTargetModule,
    PHprCommandSource Source,
    long RouteCount,
    long SafetyRejectedCount,
    long IntervalSuppressedCount,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprSafetyDecision? LastSafetyDecision,
    PHprSafetyViolation? LastSafetyViolation,
    DateTimeOffset? LastRouteAtUtc);
