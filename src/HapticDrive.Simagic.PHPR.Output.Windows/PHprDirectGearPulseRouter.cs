using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectGearPulseRoutingResult(
    bool Routed,
    string Message,
    IReadOnlyList<PHprCommandResult> OutputResults,
    DateTimeOffset CompletedAtUtc);

public sealed class PHprDirectGearPulseRouter
{
    private readonly SimagicPhprOutputDevice _output;
    private PHprRealOutputOptions _options;

    public PHprDirectGearPulseRouter(SimagicPhprOutputDevice output, PHprRealOutputOptions? options = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _options = (options ?? PHprRealOutputOptions.Disabled).Normalize();
    }

    public void Configure(PHprRealOutputOptions options)
    {
        _options = (options ?? PHprRealOutputOptions.Disabled).Normalize();
        _output.Configure(_options);
    }

    public async ValueTask<PHprDirectGearPulseRoutingResult> RouteAsync(
        ShiftIntentEvent? shiftIntentEvent,
        PHprSafetyContext safetyContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (shiftIntentEvent is null)
        {
            return Ignored("No accepted ShiftIntentEvent was supplied.");
        }

        if (!shiftIntentEvent.IsAcceptedByDrivingGate)
        {
            return Ignored("ShiftIntentEvent was suppressed by DrivingArmed/Menu Safe Mode.");
        }

        if (shiftIntentEvent.Direction == ShiftIntentDirection.Unknown)
        {
            return Ignored("ShiftIntentEvent direction is unknown.");
        }

        if (!_options.DirectControlEnabled || !_options.DirectControlArmed)
        {
            return Ignored("Real direct gear pulse routing is disabled or unarmed.");
        }

        _output.SetSafetyContext(safetyContext);
        var commands = BuildCommands(shiftIntentEvent).ToArray();
        if (commands.Length == 0)
        {
            return Ignored("All real direct gear pulse pedals are disabled.");
        }

        var results = new List<PHprCommandResult>();
        foreach (var command in commands)
        {
            results.Add(await _output.SendAsync(command, cancellationToken));
        }

        return new PHprDirectGearPulseRoutingResult(
            results.Any(result => result.Succeeded),
            results.Any(result => result.Succeeded)
                ? "Accepted ShiftIntentEvent routed to gated real P-HPR output."
                : string.Join(" ", results.Select(result => result.Message)),
            results,
            DateTimeOffset.UtcNow);
    }

    private IEnumerable<PHprCommand> BuildCommands(ShiftIntentEvent shiftIntentEvent)
    {
        var brake = _options.BrakeGearPulse.Normalize();
        if (brake.IsEnabled)
        {
            yield return PHprCommand.Create(
                PHprModuleId.Brake,
                brake.Strength01,
                brake.FrequencyHz,
                brake.DurationMs,
                PHprCommandSource.PaddleShiftIntent,
                priority: 100,
                timestampUtc: shiftIntentEvent.TimestampUtc);
        }

        var throttle = _options.ThrottleGearPulse.Normalize();
        if (throttle.IsEnabled)
        {
            yield return PHprCommand.Create(
                PHprModuleId.Throttle,
                throttle.Strength01,
                throttle.FrequencyHz,
                throttle.DurationMs,
                PHprCommandSource.PaddleShiftIntent,
                priority: 100,
                timestampUtc: shiftIntentEvent.TimestampUtc);
        }
    }

    private static PHprDirectGearPulseRoutingResult Ignored(string message)
    {
        return new PHprDirectGearPulseRoutingResult(false, message, [], DateTimeOffset.UtcNow);
    }
}
