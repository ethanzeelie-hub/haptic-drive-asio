using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprPedalEffectsRoutingSnapshot(
    PHprPedalEffectsRouterOptions Options,
    long EvaluationCount,
    long IgnoredEvaluationCount,
    PHprPedalEffectDiagnostics RoadVibration,
    PHprPedalEffectDiagnostics WheelSlip,
    PHprPedalEffectDiagnostics WheelLock,
    PHprPedalEffectKind? LastActiveEffect,
    PHprGearPulseTarget? LastTargetModule,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprSafetyDecision? LastSafetyDecision,
    PHprSafetyViolation? LastSafetyViolation,
    PHprOutputSnapshot OutputSnapshot,
    PHprSafetySnapshot SafetySnapshot,
    PHprPedalEffectsRoutingResult? LastResult,
    bool EmergencyStopActive,
    string? LastError)
{
    public IReadOnlyList<PHprPedalEffectDiagnostics> Effects => [RoadVibration, WheelSlip, WheelLock];
}
