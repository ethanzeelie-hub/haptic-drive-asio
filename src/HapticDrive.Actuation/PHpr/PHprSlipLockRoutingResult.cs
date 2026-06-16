using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprSlipLockRoutingCommandResult(
    PHprPedalEffectKind Kind,
    PHprGearPulseTarget TargetModule,
    PHprCommand Command,
    PHprCommandResult OutputResult)
{
    public bool WasRouted => OutputResult.Succeeded;
}

public sealed record PHprSlipLockRoutingResult(
    PHprSlipLockRoutingStatus Status,
    string Message,
    IReadOnlyList<PHprSlipLockRoutingCommandResult> Commands,
    PHprOutputSnapshot? OutputSnapshot,
    DateTimeOffset RoutedAtUtc,
    double Intensity01 = 0d)
{
    public bool WasRouted => Commands.Any(command => command.WasRouted);
}
