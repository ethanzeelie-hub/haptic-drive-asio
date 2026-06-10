using HapticDrive.Actuation.PHpr;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal static class PaddleGearBenchDirectPulsePlanner
{
    public static IReadOnlyList<PHprCommand> BuildCommands(
        ShiftIntentEvent shiftIntentEvent,
        PHprGearPulseTarget target,
        PHprRealGearPulseSettings brake,
        PHprRealGearPulseSettings throttle)
    {
        ArgumentNullException.ThrowIfNull(shiftIntentEvent);

        var commands = new List<PHprCommand>();
        foreach (var module in ExpandTarget(target))
        {
            var settings = module == PHprModuleId.Throttle
                ? throttle.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits)
                : brake.Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
            if (!settings.IsEnabled)
            {
                continue;
            }

            commands.Add(PHprCommand.Create(
                module,
                settings.Strength01,
                settings.FrequencyHz,
                settings.DurationMs,
                PHprCommandSource.TestBench,
                priority: 100,
                timestampUtc: shiftIntentEvent.TimestampUtc));
        }

        return commands;
    }

    private static IReadOnlyList<PHprModuleId> ExpandTarget(PHprGearPulseTarget target)
    {
        return target switch
        {
            PHprGearPulseTarget.Brake => [PHprModuleId.Brake],
            PHprGearPulseTarget.Throttle => [PHprModuleId.Throttle],
            PHprGearPulseTarget.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => [PHprModuleId.Brake]
        };
    }
}
