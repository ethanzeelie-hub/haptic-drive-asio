using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed class PHprSlipLockRouter
{
    private const int EffectCount = 2;
    private const int ModuleCount = 2;
    private const uint MaximumTelemetryFrameLag = 120;
    private readonly object _gate = new();
    private readonly IPHprOutputDevice _output;
    private readonly Action<PHprSafetyContext>? _applySafetyContext;
    private readonly long[] _routeCounts = new long[EffectCount];
    private readonly long[] _safetyRejectedCounts = new long[EffectCount];
    private readonly long[] _intervalSuppressedCounts = new long[EffectCount];
    private readonly bool[] _lastActive = new bool[EffectCount];
    private readonly double[] _lastIntensity = new double[EffectCount];
    private readonly PHprGearPulseTarget?[] _lastTargets = new PHprGearPulseTarget?[EffectCount];
    private readonly PHprCommand?[] _lastCommands = new PHprCommand?[EffectCount];
    private readonly PHprCommandResult?[] _lastOutputResults = new PHprCommandResult?[EffectCount];
    private readonly DateTimeOffset?[] _lastRouteAtUtc = new DateTimeOffset?[EffectCount];
    private readonly DateTimeOffset?[,] _lastAttemptAtUtc = new DateTimeOffset?[EffectCount, ModuleCount];
    private PHprSlipLockRouterOptions _options;
    private long _evaluationCount;
    private long _ignoredEvaluationCount;
    private PHprPedalEffectKind? _lastActiveEffect;
    private PHprGearPulseTarget? _lastTargetModule;
    private PHprCommand? _lastCommand;
    private PHprCommandResult? _lastOutputResult;
    private PHprSlipLockRoutingResult? _lastResult;
    private string? _lastError;

    public PHprSlipLockRouter(
        IPHprOutputDevice output,
        PHprSlipLockRouterOptions? options = null,
        Action<PHprSafetyContext>? applySafetyContext = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _options = (options ?? PHprSlipLockRouterOptions.Disabled).Normalize();
        _applySafetyContext = applySafetyContext;
    }

    public void Configure(PHprSlipLockRouterOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PHprSlipLockRouterOptions.Disabled).Normalize();
        }
    }

    public PHprSlipLockRoutingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new PHprSlipLockRoutingSnapshot(
                _options,
                _evaluationCount,
                _ignoredEvaluationCount,
                CreateDiagnosticsLocked(PHprPedalEffectKind.WheelSlip),
                CreateDiagnosticsLocked(PHprPedalEffectKind.WheelLock),
                _lastActiveEffect,
                _lastTargetModule,
                _lastCommand,
                _lastOutputResult,
                _lastResult,
                _output.GetSnapshot(),
                _lastError);
        }
    }

    public ValueTask<PHprSlipLockRoutingResult> RouteAsync(
        HapticPipelineSnapshot? pipelineSnapshot,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pipelineSnapshot is null)
        {
            return ValueTask.FromResult(StoreIgnored(
                PHprSlipLockRoutingStatus.IgnoredMissingVehicleState,
                "No HapticPipelineSnapshot was supplied; no P-HPR slip/lock command was sent.",
                nowUtc ?? DateTimeOffset.UtcNow));
        }

        var context = safetyContext ?? BuildContext(pipelineSnapshot);
        return RouteAsync(pipelineSnapshot.VehicleState, context, nowUtc, cancellationToken);
    }

    public async ValueTask<PHprSlipLockRoutingResult> RouteAsync(
        VehicleState? vehicleState,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PHprSlipLockRouterOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (!options.IsEnabled)
        {
            return StoreIgnored(
                PHprSlipLockRoutingStatus.IgnoredDisabled,
                "P-HPR slip/lock routing is disabled; no command was sent.",
                now);
        }

        if (vehicleState is null)
        {
            return StoreIgnored(
                PHprSlipLockRoutingStatus.IgnoredMissingVehicleState,
                "No VehicleState was supplied; no P-HPR slip/lock command was sent.",
                now);
        }

        try
        {
            var candidates = EvaluateEffects(vehicleState, options);
            StoreEvaluation(candidates);

            var plans = CreateRoutePlans(candidates, options, now);
            if (plans.Count == 0)
            {
                var anyActive = candidates.Any(candidate => candidate.IsActive);
                return StoreIgnored(
                    anyActive
                        ? PHprSlipLockRoutingStatus.IgnoredMinimumInterval
                        : PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    anyActive
                        ? "P-HPR slip/lock effects were active, but the deterministic minimum route interval suppressed this update."
                        : "No wheel slip or wheel lock effect was active; no P-HPR command was sent.",
                    now);
            }

            var context = safetyContext ?? PHprSafetyContext.DefaultMock;
            _applySafetyContext?.Invoke(context);

            var commandResults = new List<PHprSlipLockRoutingCommandResult>(plans.Count);
            foreach (var plan in plans)
            {
                var command = PHprCommand.Create(
                    plan.TargetModule.ToModuleId(),
                    plan.Strength01,
                    plan.FrequencyHz,
                    plan.DurationMs,
                    plan.Kind.ToCommandSource(),
                    plan.Priority,
                    now);
                var outputResult = await _output.SendAsync(command, cancellationToken).ConfigureAwait(false);
                commandResults.Add(new PHprSlipLockRoutingCommandResult(
                    plan.Kind,
                    plan.TargetModule,
                    outputResult.Command ?? command,
                    outputResult));
                StoreCommandResult(plan, outputResult, now);
            }

            var routedCount = commandResults.Count(result => result.WasRouted);
            var result = new PHprSlipLockRoutingResult(
                routedCount > 0
                    ? PHprSlipLockRoutingStatus.Routed
                    : PHprSlipLockRoutingStatus.RejectedBySafety,
                routedCount > 0
                    ? $"P-HPR slip/lock routed {routedCount:N0} command(s) through the selected output."
                    : "P-HPR slip/lock routing was rejected by the selected output safety gates.",
                commandResults,
                _output.GetSnapshot(),
                now);

            StoreCompleted(result, plans);
            return result;
        }
        catch (Exception ex)
        {
            var result = new PHprSlipLockRoutingResult(
                PHprSlipLockRoutingStatus.Failed,
                $"P-HPR slip/lock routing failed: {ex.Message}",
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

    private PHprSlipLockEffectRoutingDiagnostics CreateDiagnosticsLocked(PHprPedalEffectKind kind)
    {
        var index = ToIndex(kind);
        return new PHprSlipLockEffectRoutingDiagnostics(
            kind,
            _options.GetSettings(kind),
            _lastActive[index],
            _lastIntensity[index],
            _lastTargets[index],
            _routeCounts[index],
            _safetyRejectedCounts[index],
            _intervalSuppressedCounts[index],
            _lastCommands[index],
            _lastOutputResults[index],
            _lastRouteAtUtc[index]);
    }

    private static IReadOnlyList<EffectCandidate> EvaluateEffects(
        VehicleState vehicleState,
        PHprSlipLockRouterOptions options)
    {
        return
        [
            EvaluateWheelSlip(vehicleState, options.WheelSlip),
            EvaluateWheelLock(vehicleState, options.WheelLock)
        ];
    }

    private static EffectCandidate EvaluateWheelSlip(
        VehicleState vehicleState,
        PHprSlipLockEffectSettings settings)
    {
        settings = settings.Normalize(PHprPedalEffectKind.WheelSlip);
        if (!settings.IsEnabled
            || ShouldMuteForDrivingState(vehicleState)
            || !IsFresh(vehicleState, vehicleState.Telemetry, MaximumTelemetryFrameLag)
            || !IsFresh(vehicleState, vehicleState.MotionEx, MaximumTelemetryFrameLag))
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings);
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var motionEx = vehicleState.MotionEx!.Value;
        var speedScale = SpeedScale(telemetry.SpeedKph, 8f, 90f);
        if (speedScale <= 0f)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings);
        }

        var slipIntensity = 0f;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            var ratio = SanitizeMagnitude(motionEx.WheelSlipRatio[wheel], 3f);
            var angle = SanitizeMagnitude(motionEx.WheelSlipAngle[wheel], 2f);
            var ratioAmount = AmountOverThreshold(ratio, 0.08f, 0.45f);
            var angleAmount = AmountOverThreshold(angle, 0.08f, 0.45f);
            slipIntensity = Math.Max(slipIntensity, Math.Max(ratioAmount, angleAmount));
        }

        var throttle = SanitizeUnit(telemetry.Throttle);
        var brake = SanitizeUnit(telemetry.Brake);
        if (throttle < 0.1f && brake < 0.1f)
        {
            slipIntensity *= 0.35f;
        }

        if (vehicleState.CarStatus?.Value.TractionControl is > 0 && throttle >= 0.1f)
        {
            slipIntensity *= 0.75f;
        }

        var intensity = Clamp(slipIntensity * speedScale, 0f, 1f);
        return intensity <= 0f
            ? EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings)
            : new EffectCandidate(PHprPedalEffectKind.WheelSlip, settings, true, intensity);
    }

    private static EffectCandidate EvaluateWheelLock(
        VehicleState vehicleState,
        PHprSlipLockEffectSettings settings)
    {
        settings = settings.Normalize(PHprPedalEffectKind.WheelLock);
        if (!settings.IsEnabled
            || ShouldMuteForDrivingState(vehicleState)
            || !IsFresh(vehicleState, vehicleState.Telemetry, MaximumTelemetryFrameLag)
            || !IsFresh(vehicleState, vehicleState.MotionEx, MaximumTelemetryFrameLag))
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings);
        }

        var telemetry = vehicleState.Telemetry!.Value;
        var motionEx = vehicleState.MotionEx!.Value;
        var brake = SanitizeUnit(telemetry.Brake);
        if (brake < 0.1f)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings);
        }

        var speedScale = SpeedScale(telemetry.SpeedKph, 8f, 90f);
        if (speedScale <= 0f)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings);
        }

        var slipLock = 0f;
        var minimumWheelSpeed = float.MaxValue;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            var ratio = SanitizeMagnitude(motionEx.WheelSlipRatio[wheel], 3f);
            slipLock = Math.Max(slipLock, AmountOverThreshold(ratio, 0.35f, 0.45f));

            var wheelSpeed = Math.Abs(motionEx.WheelSpeed[wheel]);
            if (float.IsFinite(wheelSpeed))
            {
                minimumWheelSpeed = Math.Min(minimumWheelSpeed, wheelSpeed);
            }
        }

        var speedMetersPerSecond = telemetry.SpeedKph / 3.6f;
        var wheelLock = 0f;
        if (speedMetersPerSecond > 0.1f && minimumWheelSpeed < float.MaxValue)
        {
            var wheelSpeedRatio = Clamp(minimumWheelSpeed / speedMetersPerSecond, 0f, 1f);
            if (wheelSpeedRatio < 0.35f)
            {
                wheelLock = Clamp((0.35f - wheelSpeedRatio) / 0.35f, 0f, 1f);
            }
        }

        var intensity = Math.Max(slipLock, wheelLock) * speedScale;
        if (vehicleState.CarStatus?.Value.AntiLockBrakes is > 0)
        {
            intensity *= 0.75f;
        }

        intensity = Clamp(intensity, 0f, 1f);
        return intensity <= 0f
            ? EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings)
            : new EffectCandidate(PHprPedalEffectKind.WheelLock, settings, true, intensity);
    }

    private List<RoutePlan> CreateRoutePlans(
        IReadOnlyList<EffectCandidate> candidates,
        PHprSlipLockRouterOptions options,
        DateTimeOffset nowUtc)
    {
        var plans = new List<RoutePlan>(capacity: 2);
        var claimedBrake = false;
        var claimedThrottle = false;

        foreach (var candidate in candidates
            .Where(candidate => candidate.IsActive)
            .OrderByDescending(candidate => candidate.Settings.Priority))
        {
            var modules = ExpandTarget(candidate.Settings.TargetModule);
            var allowedModules = new List<PHprModuleId>(capacity: 2);
            foreach (var module in modules)
            {
                if ((module == PHprModuleId.Brake && claimedBrake)
                    || (module == PHprModuleId.Throttle && claimedThrottle))
                {
                    continue;
                }

                if (IsWithinMinimumInterval(candidate.Kind, module, options.MinimumRouteInterval, nowUtc))
                {
                    IncrementIntervalSuppressed(candidate.Kind);
                    continue;
                }

                allowedModules.Add(module);
            }

            if (allowedModules.Count == 0)
            {
                continue;
            }

            foreach (var module in allowedModules)
            {
                if (module == PHprModuleId.Brake)
                {
                    claimedBrake = true;
                }
                else
                {
                    claimedThrottle = true;
                }
            }

            var target = ToTarget(allowedModules);
            plans.Add(new RoutePlan(
                candidate.Kind,
                target,
                candidate.Settings.ScaleStrength(candidate.Intensity01),
                candidate.Settings.ScaleFrequency(candidate.Intensity01),
                candidate.Settings.DurationMs,
                candidate.Settings.Priority,
                allowedModules));
        }

        return plans;
    }

    private bool IsWithinMinimumInterval(
        PHprPedalEffectKind kind,
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
            var last = _lastAttemptAtUtc[ToIndex(kind), ToModuleIndex(module)];
            return last is not null && nowUtc - last.Value < interval;
        }
    }

    private void StoreEvaluation(IReadOnlyList<EffectCandidate> candidates)
    {
        lock (_gate)
        {
            _evaluationCount++;
            foreach (var candidate in candidates)
            {
                var index = ToIndex(candidate.Kind);
                _lastActive[index] = candidate.IsActive;
                _lastIntensity[index] = candidate.Intensity01;
            }
        }
    }

    private void StoreCommandResult(
        RoutePlan plan,
        PHprCommandResult outputResult,
        DateTimeOffset nowUtc)
    {
        var index = ToIndex(plan.Kind);
        lock (_gate)
        {
            foreach (var module in plan.Modules)
            {
                _lastAttemptAtUtc[index, ToModuleIndex(module)] = nowUtc;
            }

            _lastTargets[index] = plan.TargetModule;
            _lastCommands[index] = outputResult.Command;
            _lastOutputResults[index] = outputResult;

            if (outputResult.Succeeded)
            {
                _routeCounts[index]++;
                _lastRouteAtUtc[index] = nowUtc;
            }
            else
            {
                _safetyRejectedCounts[index]++;
            }
        }
    }

    private void StoreCompleted(
        PHprSlipLockRoutingResult result,
        IReadOnlyList<RoutePlan> plans)
    {
        var lastCommand = result.Commands.LastOrDefault();
        var firstPlan = plans.FirstOrDefault();
        lock (_gate)
        {
            _lastActiveEffect = firstPlan?.Kind;
            _lastTargetModule = lastCommand?.TargetModule ?? firstPlan?.TargetModule;
            _lastCommand = lastCommand?.Command;
            _lastOutputResult = lastCommand?.OutputResult;
            _lastResult = result;
            _lastError = null;
        }
    }

    private PHprSlipLockRoutingResult StoreIgnored(
        PHprSlipLockRoutingStatus status,
        string message,
        DateTimeOffset nowUtc)
    {
        var result = new PHprSlipLockRoutingResult(
            status,
            message,
            [],
            _output.GetSnapshot(),
            nowUtc);

        lock (_gate)
        {
            _ignoredEvaluationCount++;
            _lastResult = result;
            _lastError = null;
        }

        return result;
    }

    private void StoreFailed(PHprSlipLockRoutingResult result, string errorMessage)
    {
        lock (_gate)
        {
            _ignoredEvaluationCount++;
            _lastResult = result;
            _lastError = string.IsNullOrWhiteSpace(errorMessage)
                ? "Unknown P-HPR slip/lock routing error."
                : errorMessage.Trim();
        }
    }

    private void IncrementIntervalSuppressed(PHprPedalEffectKind kind)
    {
        lock (_gate)
        {
            _intervalSuppressedCounts[ToIndex(kind)]++;
        }
    }

    private static bool ShouldMuteForDrivingState(VehicleState vehicleState)
    {
        if (vehicleState.Session?.Value.GamePaused is > 0)
        {
            return true;
        }

        if (vehicleState.CarStatus?.Value.NetworkPaused is > 0)
        {
            return true;
        }

        if (vehicleState.Lap?.Value.DriverStatus == 0)
        {
            return true;
        }

        return vehicleState.Lap?.Value.ResultStatus is 0 or 1;
    }

    private static bool IsFresh<T>(
        VehicleState vehicleState,
        VehicleStateSample<T>? sample,
        uint maximumFrameLag)
    {
        if (sample is null)
        {
            return false;
        }

        var currentFrame = vehicleState.Frame.OverallFrameIdentifier;
        if (currentFrame is null || maximumFrameLag == 0)
        {
            return true;
        }

        var sampleFrame = sample.Stamp.OverallFrameIdentifier;
        if (sampleFrame > currentFrame.Value)
        {
            return true;
        }

        return currentFrame.Value - sampleFrame <= maximumFrameLag;
    }

    private static float SpeedScale(float speedKph, float minimumSpeedKph, float fullIntensitySpeedKph)
    {
        if (!float.IsFinite(speedKph) || speedKph <= minimumSpeedKph)
        {
            return 0f;
        }

        if (fullIntensitySpeedKph <= minimumSpeedKph)
        {
            return 1f;
        }

        return Clamp((speedKph - minimumSpeedKph) / (fullIntensitySpeedKph - minimumSpeedKph), 0f, 1f);
    }

    private static float SanitizeUnit(float value)
    {
        return float.IsFinite(value) ? Clamp(value, 0f, 1f) : 0f;
    }

    private static float SanitizeMagnitude(float value, float maximumMagnitude)
    {
        return float.IsFinite(value) ? Clamp(Math.Abs(value), 0f, maximumMagnitude) : 0f;
    }

    private static float AmountOverThreshold(float value, float threshold, float fullScale)
    {
        if (!float.IsFinite(value) || !float.IsFinite(threshold) || !float.IsFinite(fullScale) || fullScale <= threshold)
        {
            return 0f;
        }

        if (value <= threshold)
        {
            return 0f;
        }

        return Clamp((value - threshold) / (fullScale - threshold), 0f, 1f);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return Math.Clamp(value, minimum, maximum);
    }

    private static IReadOnlyList<PHprModuleId> ExpandTarget(PHprGearPulseTarget target)
    {
        return target switch
        {
            PHprGearPulseTarget.Brake => [PHprModuleId.Brake],
            PHprGearPulseTarget.Throttle => [PHprModuleId.Throttle],
            PHprGearPulseTarget.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => [PHprModuleId.Brake, PHprModuleId.Throttle]
        };
    }

    private static PHprGearPulseTarget ToTarget(IReadOnlyList<PHprModuleId> modules)
    {
        var hasBrake = modules.Contains(PHprModuleId.Brake);
        var hasThrottle = modules.Contains(PHprModuleId.Throttle);
        return (hasBrake, hasThrottle) switch
        {
            (true, true) => PHprGearPulseTarget.Both,
            (true, false) => PHprGearPulseTarget.Brake,
            (false, true) => PHprGearPulseTarget.Throttle,
            _ => PHprGearPulseTarget.Both
        };
    }

    private static int ToIndex(PHprPedalEffectKind kind)
    {
        return kind == PHprPedalEffectKind.WheelLock ? 1 : 0;
    }

    private static int ToModuleIndex(PHprModuleId module)
    {
        return module == PHprModuleId.Throttle ? 1 : 0;
    }

    private sealed record EffectCandidate(
        PHprPedalEffectKind Kind,
        PHprSlipLockEffectSettings Settings,
        bool IsActive,
        double Intensity01)
    {
        public static EffectCandidate Inactive(PHprPedalEffectKind kind, PHprSlipLockEffectSettings settings)
        {
            return new EffectCandidate(kind, settings, false, 0d);
        }
    }

    private sealed record RoutePlan(
        PHprPedalEffectKind Kind,
        PHprGearPulseTarget TargetModule,
        double Strength01,
        double FrequencyHz,
        int DurationMs,
        int Priority,
        IReadOnlyList<PHprModuleId> Modules);
}
