using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed class PHprRoadVibrationRouter
{
    private readonly object _gate = new();
    private readonly IPHprOutputDevice _output;
    private readonly Action<PHprSafetyContext>? _applySafetyContext;
    private readonly RoadTextureEvaluator _evaluator = new();
    private PHprRoadVibrationRouterOptions _options;
    private DateTimeOffset? _lastBrakeAttemptAtUtc;
    private DateTimeOffset? _lastThrottleAttemptAtUtc;
    private DateTimeOffset? _lastGearPulseAtUtc;
    private long _evaluationCount;
    private long _ignoredEvaluationCount;
    private long _routeCount;
    private long _safetyRejectedCount;
    private long _intervalSuppressedCount;
    private bool _lastActive;
    private double _lastIntensity01;
    private RoadTextureSignal _lastSignal = RoadTextureSignal.Inactive(DateTimeOffset.UtcNow, "not evaluated");
    private PHprCommand? _lastCommand;
    private PHprCommandResult? _lastOutputResult;
    private PHprRoadVibrationRoutingResult? _lastResult;
    private string? _lastError;

    public PHprRoadVibrationRouter(
        IPHprOutputDevice output,
        PHprRoadVibrationRouterOptions? options = null,
        Action<PHprSafetyContext>? applySafetyContext = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _options = (options ?? PHprRoadVibrationRouterOptions.Disabled).Normalize();
        _applySafetyContext = applySafetyContext;
    }

    public void Configure(PHprRoadVibrationRouterOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PHprRoadVibrationRouterOptions.Disabled).Normalize();
        }
    }

    public void NotifyGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        lock (_gate)
        {
            _lastGearPulseAtUtc = timestampUtc ?? DateTimeOffset.UtcNow;
        }
    }

    public PHprRoadVibrationRoutingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new PHprRoadVibrationRoutingSnapshot(
                _options,
                _evaluationCount,
                _ignoredEvaluationCount,
                _routeCount,
                _safetyRejectedCount,
                _intervalSuppressedCount,
                _lastActive,
                _lastIntensity01,
                _lastSignal,
                _lastCommand,
                _lastOutputResult,
                _lastResult,
                _output.GetSnapshot(),
                _lastError);
        }
    }

    public ValueTask<PHprRoadVibrationRoutingResult> RouteAsync(
        HapticPipelineSnapshot? pipelineSnapshot,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pipelineSnapshot is null)
        {
            return ValueTask.FromResult(StoreIgnored(
                PHprRoadVibrationRoutingStatus.IgnoredMissingVehicleState,
                "No HapticPipelineSnapshot was supplied; no P-HPR road-vibration command was sent.",
                nowUtc ?? DateTimeOffset.UtcNow));
        }

        var context = safetyContext ?? BuildContext(pipelineSnapshot);
        return RouteAsync(pipelineSnapshot.Effects.RoadTexture.Signal, context, nowUtc, cancellationToken);
    }

    public async ValueTask<PHprRoadVibrationRoutingResult> RouteAsync(
        VehicleState? vehicleState,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PHprRoadVibrationRouterOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (!options.IsEnabled)
        {
            return StoreIgnored(
                PHprRoadVibrationRoutingStatus.IgnoredDisabled,
                "P-HPR road vibration routing is disabled; no command was sent.",
                now);
        }

        if (vehicleState is null)
        {
            return StoreIgnored(
                PHprRoadVibrationRoutingStatus.IgnoredMissingVehicleState,
                "No VehicleState was supplied; no P-HPR road-vibration command was sent.",
                now);
        }

        try
        {
            var signal = _evaluator.Evaluate(
                vehicleState,
                BuildEvaluationContext(safetyContext, now));
            return await RouteAsync(signal, safetyContext, now, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var result = new PHprRoadVibrationRoutingResult(
                PHprRoadVibrationRoutingStatus.Failed,
                $"P-HPR road-vibration routing failed: {ex.Message}",
                [],
                _output.GetSnapshot(),
                now);
            StoreFailed(result, ex.Message);
            return result;
        }
    }

    public async ValueTask<PHprRoadVibrationRoutingResult> RouteAsync(
        RoadTextureSignal? signal,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PHprRoadVibrationRouterOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (!options.IsEnabled)
        {
            return StoreIgnored(
                PHprRoadVibrationRoutingStatus.IgnoredDisabled,
                "P-HPR road vibration routing is disabled; no command was sent.",
                now);
        }

        if (signal is null)
        {
            return StoreIgnored(
                PHprRoadVibrationRoutingStatus.IgnoredMissingVehicleState,
                "No RoadTextureSignal was supplied; no P-HPR road-vibration command was sent.",
                now);
        }

        try
        {
            if (safetyContext?.HapticsStopped == true)
            {
                return StoreIgnored(
                    PHprRoadVibrationRoutingStatus.IgnoredNoActiveRoadVibration,
                    "P-HPR road vibration was dropped because haptics are stopped.",
                    now);
            }

            if (safetyContext?.TelemetryStale == true)
            {
                return StoreIgnored(
                    PHprRoadVibrationRoutingStatus.IgnoredNoActiveRoadVibration,
                    "P-HPR road vibration was dropped because telemetry is stale.",
                    now);
            }

            StoreEvaluation(signal);
            if (!signal.IsActive)
            {
                return StoreIgnored(
                    PHprRoadVibrationRoutingStatus.IgnoredNoActiveRoadVibration,
                    $"No active road vibration was detected; no P-HPR road command was sent. Reason: {signal.SuppressedReason ?? "inactive signal"}.",
                    now,
                    signal.OutputIntensity);
            }

            if (signal.GearDuckingActive)
            {
                return StoreIgnored(
                    PHprRoadVibrationRoutingStatus.IgnoredGearDucking,
                    "P-HPR road vibration was suppressed because a higher-priority gear pulse is inside the ducking window.",
                    now,
                    signal.OutputIntensity);
            }

            var intensity = signal.OutputIntensity;
            var plans = CreatePlans(options, intensity, now);
            if (plans.Count == 0)
            {
                return StoreIgnored(
                    options.Brake.IsEnabled || options.Throttle.IsEnabled
                        ? PHprRoadVibrationRoutingStatus.IgnoredMinimumInterval
                        : PHprRoadVibrationRoutingStatus.IgnoredNoEnabledPedal,
                    options.Brake.IsEnabled || options.Throttle.IsEnabled
                        ? "P-HPR road vibration was active, but the deterministic minimum route interval suppressed this update."
                        : "P-HPR road vibration has no enabled pedal target.",
                    now,
                    intensity);
            }

            var context = safetyContext ?? PHprSafetyContext.DefaultMock;
            _applySafetyContext?.Invoke(context);

            var commandResults = new List<PHprRoadVibrationCommandResult>(plans.Count);
            foreach (var plan in plans)
            {
                var command = PHprCommand.Create(
                    plan.TargetModule,
                    plan.Settings.ScaleStrength(intensity),
                    ScaleFrequency(plan.Settings, signal, intensity),
                    plan.Settings.DurationMs,
                    PHprCommandSource.RoadTexture,
                    options.Priority,
                    now);
                var outputResult = await _output.SendAsync(command, cancellationToken).ConfigureAwait(false);
                commandResults.Add(new PHprRoadVibrationCommandResult(
                    plan.TargetModule,
                    outputResult.Command ?? command,
                    outputResult));
                StoreCommandResult(plan.TargetModule, outputResult, now);
            }

            var routedCount = commandResults.Count(result => result.WasRouted);
            var outputSnapshot = _output.GetSnapshot();
            var result = new PHprRoadVibrationRoutingResult(
                routedCount > 0
                    ? PHprRoadVibrationRoutingStatus.Routed
                    : PHprRoadVibrationRoutingStatus.RejectedBySafety,
                routedCount > 0
                    ? $"P-HPR road vibration routed {routedCount:N0} command(s) through the selected output."
                    : "P-HPR road vibration was rejected by the selected output safety gates.",
                commandResults,
                outputSnapshot,
                now,
                intensity);

            StoreCompleted(result);
            return result;
        }
        catch (Exception ex)
        {
            var result = new PHprRoadVibrationRoutingResult(
                PHprRoadVibrationRoutingStatus.Failed,
                $"P-HPR road-vibration routing failed: {ex.Message}",
                [],
                _output.GetSnapshot(),
                now);
            StoreFailed(result, ex.Message);
            return result;
        }
    }

    private static PHprSafetyContext BuildContext(HapticPipelineSnapshot snapshot)
    {
        return PHprSafetyContext.DefaultMock with
        {
            TelemetryStale = snapshot.TelemetryTimedOutMuted,
            HapticsStopped = !snapshot.IsRunning,
            EmergencyMuteActive = snapshot.EmergencyMute
        };
    }

    private RoadTextureEvaluationContext BuildEvaluationContext(
        PHprSafetyContext? safetyContext,
        DateTimeOffset nowUtc)
    {
        DateTimeOffset? lastGearPulseAtUtc;
        lock (_gate)
        {
            lastGearPulseAtUtc = _lastGearPulseAtUtc;
        }

        var context = safetyContext ?? PHprSafetyContext.DefaultMock;
        return new RoadTextureEvaluationContext(
            nowUtc,
            HapticsRunning: !context.HapticsStopped,
            DrivingArmed: context.DrivingArmed,
            AllowWhenDrivingNotArmed: false,
            TelemetryStale: context.TelemetryStale,
            lastGearPulseAtUtc);
    }

    private List<RoutePlan> CreatePlans(
        PHprRoadVibrationRouterOptions options,
        double intensity01,
        DateTimeOffset nowUtc)
    {
        var plans = new List<RoutePlan>(capacity: 2);
        if (options.Brake.IsEnabled)
        {
            if (IsWithinMinimumInterval(PHprModuleId.Brake, options.MinimumRouteInterval, nowUtc))
            {
                IncrementIntervalSuppressed();
            }
            else
            {
                plans.Add(new RoutePlan(PHprModuleId.Brake, options.Brake.Normalize()));
            }
        }

        if (options.Throttle.IsEnabled)
        {
            if (IsWithinMinimumInterval(PHprModuleId.Throttle, options.MinimumRouteInterval, nowUtc))
            {
                IncrementIntervalSuppressed();
            }
            else
            {
                plans.Add(new RoutePlan(PHprModuleId.Throttle, options.Throttle.Normalize()));
            }
        }

        return plans;
    }

    private bool IsWithinMinimumInterval(
        PHprModuleId module,
        TimeSpan interval,
        DateTimeOffset nowUtc)
    {
        if (interval <= TimeSpan.Zero)
        {
            return false;
        }

        lock (_gate)
        {
            var last = module == PHprModuleId.Throttle
                ? _lastThrottleAttemptAtUtc
                : _lastBrakeAttemptAtUtc;
            return last is not null && nowUtc - last.Value < interval;
        }
    }

    private void StoreEvaluation(RoadTextureSignal signal)
    {
        lock (_gate)
        {
            _evaluationCount++;
            _lastActive = signal.IsActive;
            _lastIntensity01 = signal.OutputIntensity;
            _lastSignal = signal;
        }
    }

    private void StoreCommandResult(
        PHprModuleId module,
        PHprCommandResult outputResult,
        DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (module == PHprModuleId.Throttle)
            {
                _lastThrottleAttemptAtUtc = nowUtc;
            }
            else
            {
                _lastBrakeAttemptAtUtc = nowUtc;
            }

            _lastCommand = outputResult.Command;
            _lastOutputResult = outputResult;
            if (outputResult.Succeeded)
            {
                _routeCount++;
            }
            else
            {
                _safetyRejectedCount++;
            }
        }
    }

    private void StoreCompleted(PHprRoadVibrationRoutingResult result)
    {
        lock (_gate)
        {
            _lastResult = result;
            _lastError = null;
        }
    }

    private PHprRoadVibrationRoutingResult StoreIgnored(
        PHprRoadVibrationRoutingStatus status,
        string message,
        DateTimeOffset nowUtc,
        double intensity01 = 0d)
    {
        var result = new PHprRoadVibrationRoutingResult(
            status,
            message,
            [],
            _output.GetSnapshot(),
            nowUtc,
            intensity01);

        lock (_gate)
        {
            _ignoredEvaluationCount++;
            _lastResult = result;
            _lastError = null;
        }

        return result;
    }

    private void StoreFailed(PHprRoadVibrationRoutingResult result, string errorMessage)
    {
        lock (_gate)
        {
            _ignoredEvaluationCount++;
            _lastResult = result;
            _lastError = string.IsNullOrWhiteSpace(errorMessage)
                ? "Unknown P-HPR road-vibration routing error."
                : errorMessage.Trim();
        }
    }

    private void IncrementIntervalSuppressed()
    {
        lock (_gate)
        {
            _intervalSuppressedCount++;
        }
    }

    private static double ScaleFrequency(
        PHprRoadVibrationPedalSettings settings,
        RoadTextureSignal signal,
        double intensity01)
    {
        if (signal.PHprFrequencyHz <= 0d)
        {
            return settings.ScaleFrequency(intensity01);
        }

        var normalized = settings.Normalize();
        return Math.Clamp(
            signal.PHprFrequencyHz,
            normalized.MinimumFrequencyHz,
            normalized.FrequencyHz);
    }

    private sealed record RoutePlan(PHprModuleId TargetModule, PHprRoadVibrationPedalSettings Settings);
}
