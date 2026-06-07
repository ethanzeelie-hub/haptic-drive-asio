using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed class PHprGearPulseRouter
{
    private readonly object _gate = new();
    private readonly SafetyLimitedPhprOutputDevice _output;
    private PHprGearPulseRouterOptions _options;
    private long _acceptedRouteCount;
    private long _ignoredRouteCount;
    private long _safetyRejectedCount;
    private ShiftIntentDirection _lastShiftDirection = ShiftIntentDirection.Unknown;
    private PHprGearPulseTarget? _lastTargetModule;
    private PHprCommand? _lastCommand;
    private PHprCommandResult? _lastOutputResult;
    private PHprGearPulseRoutingResult? _lastResult;
    private string? _lastError;

    public PHprGearPulseRouter(
        SafetyLimitedPhprOutputDevice output,
        PHprGearPulseRouterOptions? options = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _options = (options ?? PHprGearPulseRouterOptions.Default).Normalize();
    }

    public void Configure(PHprGearPulseRouterOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PHprGearPulseRouterOptions.Default).Normalize();
        }
    }

    public PHprGearPulseRoutingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var outputSnapshot = _output.GetSnapshot();
            var safetySnapshot = _output.SafetySnapshot;
            return new PHprGearPulseRoutingSnapshot(
                _options,
                _acceptedRouteCount,
                _ignoredRouteCount,
                _safetyRejectedCount,
                _lastShiftDirection,
                _lastTargetModule,
                _lastCommand,
                _lastOutputResult,
                safetySnapshot.LastDecision,
                safetySnapshot.LastViolation,
                outputSnapshot,
                safetySnapshot,
                _lastResult,
                outputSnapshot.IsEmergencyStopActive || safetySnapshot.IsEmergencyStopActive,
                _lastError);
        }
    }

    public async ValueTask<PHprGearPulseRoutingResult> RouteAsync(
        ShiftIntentEvent? shiftIntentEvent,
        PHprSafetyContext? safetyContext = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PHprGearPulseRouterOptions options;
        lock (_gate)
        {
            options = _options;
        }

        if (!options.IsEnabled)
        {
            return StoreIgnored(
                PHprGearPulseRoutingStatus.IgnoredDisabled,
                "Mock P-HPR gear pulse routing is disabled; no command was sent.",
                shiftIntentEvent);
        }

        if (shiftIntentEvent is null)
        {
            return StoreIgnored(
                PHprGearPulseRoutingStatus.IgnoredMissingShiftIntent,
                "No accepted ShiftIntentEvent was supplied; no mock P-HPR command was sent.",
                null);
        }

        if (!shiftIntentEvent.IsAcceptedByDrivingGate)
        {
            return StoreIgnored(
                PHprGearPulseRoutingStatus.IgnoredDrivingNotArmed,
                "ShiftIntentEvent was not accepted by DrivingArmed; no mock P-HPR command was sent.",
                shiftIntentEvent);
        }

        if (shiftIntentEvent.Direction == ShiftIntentDirection.Unknown)
        {
            return StoreIgnored(
                PHprGearPulseRoutingStatus.IgnoredUnknownDirection,
                "ShiftIntentEvent direction is unknown; no mock P-HPR command was sent.",
                shiftIntentEvent);
        }

        try
        {
            var profile = options.Profile.Normalize();
            var command = PHprCommand.Create(
                options.TargetModule.ToModuleId(),
                profile.Strength01,
                profile.FrequencyHz,
                profile.DurationMs,
                PHprCommandSource.PaddleShiftIntent,
                profile.Priority,
                shiftIntentEvent.TimestampUtc,
                PHprSafetyFlags.MockOnly);
            var context = BuildContext(shiftIntentEvent, safetyContext);

            _output.SetSafetyContext(context);
            var outputResult = await _output.SendAsync(command, cancellationToken);
            var outputSnapshot = _output.GetSnapshot();
            var safetySnapshot = _output.SafetySnapshot;
            var status = outputResult.Succeeded
                ? PHprGearPulseRoutingStatus.Routed
                : PHprGearPulseRoutingStatus.RejectedBySafety;
            var result = new PHprGearPulseRoutingResult(
                status,
                outputResult.Succeeded
                    ? "Accepted ShiftIntentEvent routed to safety-limited mock P-HPR output; no hardware write was performed."
                    : outputResult.Message,
                shiftIntentEvent,
                outputResult.Command ?? command,
                outputResult,
                safetySnapshot,
                outputSnapshot,
                DateTimeOffset.UtcNow);

            StoreCompleted(result, options.TargetModule);
            return result;
        }
        catch (Exception ex)
        {
            var result = new PHprGearPulseRoutingResult(
                PHprGearPulseRoutingStatus.Failed,
                $"Mock P-HPR gear pulse routing failed: {ex.Message}",
                shiftIntentEvent,
                null,
                null,
                _output.SafetySnapshot,
                _output.GetSnapshot(),
                DateTimeOffset.UtcNow);
            StoreFailed(result, ex.Message);
            return result;
        }
    }

    public async ValueTask<PHprGearPulseRoutingResult> EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _output.EmergencyStopAsync(cancellationToken);
        var outputSnapshot = _output.GetSnapshot();
        var safetySnapshot = _output.SafetySnapshot;
        var result = new PHprGearPulseRoutingResult(
            PHprGearPulseRoutingStatus.EmergencyStopped,
            "Mock P-HPR emergency stop activated through the safety-limited output; no hardware write was performed.",
            null,
            outputSnapshot.LastCommand,
            PHprCommandResult.Accepted(outputSnapshot.LastCommand!, "Mock emergency stop recorded."),
            safetySnapshot,
            outputSnapshot,
            DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _lastCommand = result.Command;
            _lastOutputResult = result.OutputResult;
            _lastTargetModule = PHprGearPulseTarget.Both;
            _lastResult = result;
            _lastError = null;
        }

        return result;
    }

    public void ClearEmergencyStop()
    {
        _output.ClearEmergencyStop();
    }

    public void ClearDiagnostics()
    {
        lock (_gate)
        {
            _acceptedRouteCount = 0;
            _ignoredRouteCount = 0;
            _safetyRejectedCount = 0;
            _lastShiftDirection = ShiftIntentDirection.Unknown;
            _lastTargetModule = null;
            _lastCommand = null;
            _lastOutputResult = null;
            _lastResult = null;
            _lastError = null;
        }

        _output.Inner.ClearHistory();
        _output.ResetSafetyState();
    }

    private static PHprSafetyContext BuildContext(
        ShiftIntentEvent shiftIntentEvent,
        PHprSafetyContext? safetyContext)
    {
        var context = safetyContext ?? PHprSafetyContext.DefaultMock with
        {
            DrivingArmed = shiftIntentEvent.DrivingArmedAtEvent.IsArmed
        };

        return context with
        {
            IsMockOutput = true,
            RequiresRealDeviceWrites = false,
            SoftwareConflictStatus = context.SoftwareConflictStatus == PHprSoftwareConflictStatus.Unknown
                ? PHprSoftwareConflictStatus.Clear
                : context.SoftwareConflictStatus
        };
    }

    private PHprGearPulseRoutingResult StoreIgnored(
        PHprGearPulseRoutingStatus status,
        string message,
        ShiftIntentEvent? shiftIntentEvent)
    {
        var result = new PHprGearPulseRoutingResult(
            status,
            message,
            shiftIntentEvent,
            null,
            null,
            _output.SafetySnapshot,
            _output.GetSnapshot(),
            DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _ignoredRouteCount++;
            _lastShiftDirection = shiftIntentEvent?.Direction ?? ShiftIntentDirection.Unknown;
            _lastResult = result;
            _lastError = null;
        }

        return result;
    }

    private void StoreCompleted(
        PHprGearPulseRoutingResult result,
        PHprGearPulseTarget target)
    {
        lock (_gate)
        {
            if (result.WasRouted)
            {
                _acceptedRouteCount++;
            }
            else
            {
                _safetyRejectedCount++;
            }

            _lastShiftDirection = result.ShiftIntentEvent?.Direction ?? ShiftIntentDirection.Unknown;
            _lastTargetModule = target;
            _lastCommand = result.Command;
            _lastOutputResult = result.OutputResult;
            _lastResult = result;
            _lastError = null;
        }
    }

    private void StoreFailed(PHprGearPulseRoutingResult result, string errorMessage)
    {
        lock (_gate)
        {
            _ignoredRouteCount++;
            _lastShiftDirection = result.ShiftIntentEvent?.Direction ?? ShiftIntentDirection.Unknown;
            _lastResult = result;
            _lastError = string.IsNullOrWhiteSpace(errorMessage)
                ? "Unknown mock gear pulse routing error."
                : errorMessage.Trim();
        }
    }
}
