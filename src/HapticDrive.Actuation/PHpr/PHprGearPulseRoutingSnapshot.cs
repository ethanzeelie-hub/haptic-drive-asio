using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprGearPulseRoutingSnapshot(
    PHprGearPulseRouterOptions Options,
    long AcceptedRouteCount,
    long IgnoredRouteCount,
    long SafetyRejectedCount,
    ShiftIntentDirection LastShiftDirection,
    PHprGearPulseTarget? LastTargetModule,
    PHprCommand? LastCommand,
    PHprCommandResult? LastOutputResult,
    PHprSafetyDecision? LastSafetyDecision,
    PHprSafetyViolation? LastSafetyViolation,
    PHprOutputSnapshot OutputSnapshot,
    PHprSafetySnapshot SafetySnapshot,
    PHprGearPulseRoutingResult? LastResult,
    bool EmergencyStopActive,
    string? LastError);
