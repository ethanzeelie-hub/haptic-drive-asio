using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprPedalEffectRoutingCommandResult(
    PHprPedalEffectKind Kind,
    PHprGearPulseTarget TargetModule,
    PHprCommand Command,
    PHprCommandResult OutputResult)
{
    public bool WasRouted => OutputResult.Succeeded;
}

public sealed record PHprPedalEffectsRoutingResult(
    PHprPedalEffectsRoutingStatus Status,
    string Message,
    IReadOnlyList<PHprPedalEffectRoutingCommandResult> Commands,
    PHprSafetySnapshot? SafetySnapshot,
    PHprOutputSnapshot? OutputSnapshot,
    DateTimeOffset RoutedAtUtc)
{
    public bool WasRouted => Commands.Any(command => command.WasRouted);
}
