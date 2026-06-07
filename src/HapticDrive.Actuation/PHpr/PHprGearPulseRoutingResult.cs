using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprGearPulseRoutingResult(
    PHprGearPulseRoutingStatus Status,
    string Message,
    ShiftIntentEvent? ShiftIntentEvent,
    PHprCommand? Command,
    PHprCommandResult? OutputResult,
    PHprSafetySnapshot? SafetySnapshot,
    PHprOutputSnapshot? OutputSnapshot,
    DateTimeOffset RoutedAtUtc)
{
    public bool WasRouted => Status == PHprGearPulseRoutingStatus.Routed;
}
