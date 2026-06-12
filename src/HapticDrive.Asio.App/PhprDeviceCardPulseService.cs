using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Output.Windows;

namespace HapticDrive.Asio.App;

internal interface IPHprDirectPulseService
{
    string InstanceId { get; }

    string OutputInstanceId { get; }

    string WriterInstanceId { get; }

    string EncoderInstanceId { get; }

    string StopMethodId { get; }

    ValueTask<PhprDeviceCardPulseResult> SendDirectPulseAsync(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        DateTimeOffset? timestampUtc = null,
        CancellationToken cancellationToken = default);
}

internal sealed record PhprDeviceCardPulseResult(
    PHprModuleId ModuleId,
    PHprRealGearPulseSettings Settings,
    PHprCommand Command,
    PHprCommandResult CommandResult,
    string RouteName)
{
    public bool Succeeded => CommandResult.Status == PHprCommandStatus.Accepted;
}

internal sealed class PhprDeviceCardPulseService : IPHprDirectPulseService
{
    public const string RouteName = "DevicesTabTestPulse";

    private readonly SimagicPhprOutputDevice _output;

    public PhprDeviceCardPulseService(SimagicPhprOutputDevice output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        InstanceId = $"pulse-service-{Guid.NewGuid():N}";
    }

    public string InstanceId { get; }

    public string OutputInstanceId => _output.InstanceId;

    public string WriterInstanceId => _output.WriterInstanceId;

    public string EncoderInstanceId => _output.EncoderInstanceId;

    public string StopMethodId => _output.StopMethodId;

    public async ValueTask<PhprDeviceCardPulseResult> SendDirectPulseAsync(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        DateTimeOffset? timestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = (settings ?? PHprRealGearPulseSettings.Default)
            .Normalize(SimagicPhprOutputDevice.DirectControlSafetyLimits);
        _output.SetSafetyContext(safetyContext);
        var command = CreateDirectPulseCommand(moduleId, normalized, timestampUtc);
        var result = await _output.SendAsync(command, cancellationToken).ConfigureAwait(false);
        return new PhprDeviceCardPulseResult(moduleId, normalized, command, result, RouteName);
    }

    public static async ValueTask<PhprDeviceCardPulseResult> SendDirectPulseAsync(
        SimagicPhprOutputDevice output,
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings,
        PHprSafetyContext safetyContext,
        DateTimeOffset? timestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var service = new PhprDeviceCardPulseService(output);
        return await service.SendDirectPulseAsync(
            moduleId,
            settings,
            safetyContext,
            timestampUtc,
            cancellationToken).ConfigureAwait(false);
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
