using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprSlipLockEffectRoutingDiagnostics(
    PHprPedalEffectKind Kind,
    PHprSlipLockEffectSettings Settings,
    bool LastActive,
    double LastIntensity01,
    PHprGearPulseTarget? LastTargetModule,
    long RouteCount,
    long SafetyRejectedCount,
    long IntervalSuppressedCount,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    DateTimeOffset? LastRouteAtUtc);

public sealed record PHprSlipLockRoutingSnapshot(
    PHprSlipLockRouterOptions Options,
    long EvaluationCount,
    long IgnoredEvaluationCount,
    PHprSlipLockEffectRoutingDiagnostics WheelSlip,
    PHprSlipLockEffectRoutingDiagnostics WheelLock,
    PHprPedalEffectKind? LastActiveEffect,
    PHprGearPulseTarget? LastTargetModule,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprSlipLockRoutingResult? LastResult,
    PHprOutputSnapshot? OutputSnapshot,
    string? LastError)
{
    public IReadOnlyList<PHprSlipLockEffectRoutingDiagnostics> Effects => [WheelSlip, WheelLock];
}
