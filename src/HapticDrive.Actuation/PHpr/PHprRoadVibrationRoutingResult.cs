using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;

namespace HapticDrive.Actuation.PHpr;

public sealed record PHprRoadVibrationCommandResult(
    PHprModuleId TargetModule,
    PHprCommand Command,
    PHprCommandResult OutputResult)
{
    public bool WasRouted => OutputResult.Succeeded;
}

public sealed record PHprRoadVibrationRoutingResult(
    PHprRoadVibrationRoutingStatus Status,
    string Message,
    IReadOnlyList<PHprRoadVibrationCommandResult> Commands,
    PHprOutputSnapshot? OutputSnapshot,
    DateTimeOffset RoutedAtUtc,
    double Intensity01 = 0d)
{
    public bool WasRouted => Commands.Any(command => command.WasRouted);
}
