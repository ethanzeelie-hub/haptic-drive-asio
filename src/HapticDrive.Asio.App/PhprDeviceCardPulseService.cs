using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal sealed record PhprDeviceCardPulseResult(
    PHprModuleId ModuleId,
    PHprRealGearPulseSettings Settings,
    PHprCommand Command,
    PHprCommandResult CommandResult,
    string RouteName)
{
    public bool Succeeded => CommandResult.Status == PHprCommandStatus.Accepted;
}

internal static class PhprDeviceCardPulseService
{
    public const string RouteName = "DevicesTabTestPulse";

    public static async ValueTask<PhprDeviceCardPulseResult> SendDirectPulseAsync(
        SimagicPhprOutputDevice output,
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        DateTimeOffset? timestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var normalized = (settings ?? PHprRealGearPulseSettings.Default)
            .Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        output.SetSafetyContext(safetyContext);
        var command = CreateDirectPulseCommand(moduleId, normalized, timestampUtc);
        var result = await output.SendAsync(command, cancellationToken).ConfigureAwait(false);
        return new PhprDeviceCardPulseResult(moduleId, normalized, command, result, RouteName);
    }

    public static PHprCommand CreateDirectPulseCommand(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        DateTimeOffset? timestampUtc = null)
    {
        var normalized = (settings ?? PHprRealGearPulseSettings.Default)
            .Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        return PHprCommand.Create(
            moduleId,
            normalized.Strength01,
            normalized.FrequencyHz,
            normalized.DurationMs,
            PHprCommandSource.TestBench,
            priority: 100,
            timestampUtc: timestampUtc);
    }
}
