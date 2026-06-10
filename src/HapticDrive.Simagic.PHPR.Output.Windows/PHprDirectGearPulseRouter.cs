using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectGearPulseCommandTrace(
    PHprCommand Command,
    DateTimeOffset CommandCreatedAtUtc,
    DateTimeOffset? WriteCompletedAtUtc,
    PHprCommandResult Result);

public sealed record PHprDirectGearPulseRoutingResult(
    bool Routed,
    string Message,
    IReadOnlyList<PHprCommandResult> OutputResults,
    DateTimeOffset CompletedAtUtc,
    ShiftIntentEvent? ShiftIntentEvent = null,
    DateTimeOffset? PaddleEventAtUtc = null,
    DateTimeOffset? ShiftIntentAcceptedAtUtc = null,
    DateTimeOffset? FirstCommandCreatedAtUtc = null,
    DateTimeOffset? FirstWriteCompletedAtUtc = null,
    IReadOnlyList<PHprDirectGearPulseCommandTrace>? CommandTraces = null);

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
            return Ignored("ShiftIntentEvent was suppressed by DrivingArmed/Menu Safe Mode.", shiftIntentEvent);
        }

        if (shiftIntentEvent.Direction == ShiftIntentDirection.Unknown)
        {
            return Ignored("ShiftIntentEvent direction is unknown.", shiftIntentEvent);
        }

        if (!_options.DirectControlEnabled || !_options.DirectControlArmed)
        {
            return Ignored("Real direct gear pulse routing is disabled or unarmed.", shiftIntentEvent);
        }

        if (!_options.DirectControlApprovalConfirmed)
        {
            return Ignored("Real direct gear pulse routing is missing the exact controlled-write approval phrase.", shiftIntentEvent);
        }

        if (_options.CandidateIsRawInputOnly || !_options.CandidateHasOpenableHidPath)
        {
            return Ignored("Real direct gear pulse routing requires an openable HID device-interface candidate, not Raw Input metadata.", shiftIntentEvent);
        }

        if (!_options.OpenCheckSucceeded)
        {
            return Ignored("Real direct gear pulse routing requires a successful HID open-check for the selected candidate.", shiftIntentEvent);
        }

        if (!_options.AllowsDirectPulseReportShape)
        {
            return Ignored("Real direct gear pulse routing requires known HID output-report capability or successful no-command report-shape validation.", shiftIntentEvent);
        }

        _output.SetSafetyContext(safetyContext);
        var commands = BuildCommands(shiftIntentEvent).ToArray();
        if (commands.Length == 0)
        {
            return Ignored("All real direct gear pulse pedals are disabled.", shiftIntentEvent);
        }

        var results = new List<PHprCommandResult>();
        var traces = new List<PHprDirectGearPulseCommandTrace>();
        foreach (var command in commands)
        {
            var result = await _output.SendAsync(command.Command, cancellationToken);
            results.Add(result);
            var diagnostics = _output.GetDiagnostics();
            traces.Add(new PHprDirectGearPulseCommandTrace(
                command.Command,
                command.CommandCreatedAtUtc,
                diagnostics.Connection.LastWriteAtUtc,
                result));
        }

        return new PHprDirectGearPulseRoutingResult(
            results.Any(result => result.Succeeded),
            results.Any(result => result.Succeeded)
                ? "Accepted ShiftIntentEvent routed to gated real P-HPR output."
                : string.Join(" ", results.Select(result => result.Message)),
            results,
            DateTimeOffset.UtcNow,
            shiftIntentEvent,
            shiftIntentEvent.TimestampUtc,
            shiftIntentEvent.AcceptedAtUtc ?? shiftIntentEvent.TimestampUtc,
            traces.FirstOrDefault()?.CommandCreatedAtUtc,
            traces.FirstOrDefault()?.WriteCompletedAtUtc,
            traces);
    }

    private IEnumerable<DirectGearPulseCommand> BuildCommands(ShiftIntentEvent shiftIntentEvent)
    {
        var brake = _options.BrakeGearPulse.Normalize();
        if (brake.IsEnabled)
        {
            yield return CreateCommand(PHprModuleId.Brake, brake);
        }

        var throttle = _options.ThrottleGearPulse.Normalize();
        if (throttle.IsEnabled)
        {
            yield return CreateCommand(PHprModuleId.Throttle, throttle);
        }
    }

    private static DirectGearPulseCommand CreateCommand(
        PHprModuleId moduleId,
        PHprRealGearPulseSettings settings)
    {
        var commandCreatedAtUtc = DateTimeOffset.UtcNow;
        return new DirectGearPulseCommand(
            PHprCommand.Create(
                moduleId,
                settings.Strength01,
                settings.FrequencyHz,
                settings.DurationMs,
                PHprCommandSource.PaddleShiftIntent,
                priority: 100,
                timestampUtc: commandCreatedAtUtc),
            commandCreatedAtUtc);
    }

    private static PHprDirectGearPulseRoutingResult Ignored(string message, ShiftIntentEvent? shiftIntentEvent = null)
    {
        return new PHprDirectGearPulseRoutingResult(
            false,
            message,
            [],
            DateTimeOffset.UtcNow,
            shiftIntentEvent,
            shiftIntentEvent?.TimestampUtc,
            shiftIntentEvent?.AcceptedAtUtc ?? shiftIntentEvent?.TimestampUtc,
            CommandTraces: []);
    }

    private sealed record DirectGearPulseCommand(PHprCommand Command, DateTimeOffset CommandCreatedAtUtc);
}
