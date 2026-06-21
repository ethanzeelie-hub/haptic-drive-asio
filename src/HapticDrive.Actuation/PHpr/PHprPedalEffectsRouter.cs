using HapticDrive.Actuation.Driving;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Actuation.PHpr;

public sealed class PHprPedalEffectsRouter
{
    private const uint MaximumTelemetryFrameLag = 120;
    private readonly object _gate = new();
    private readonly SafetyLimitedPhprOutputDevice _output;
    private readonly SlipLockEvaluator _slipLockEvaluator = new();
    private readonly long[] _routeCounts = new long[EffectCount];
    private readonly long[] _safetyRejectedCounts = new long[EffectCount];
    private readonly long[] _intervalSuppressedCounts = new long[EffectCount];
    private readonly bool[] _lastActive = new bool[EffectCount];
    private readonly double[] _lastIntensity = new double[EffectCount];
    private readonly PHprGearPulseTarget?[] _lastTargets = new PHprGearPulseTarget?[EffectCount];
    private readonly PHprCommand?[] _lastCommands = new PHprCommand?[EffectCount];
    private readonly PHprCommandResult?[] _lastOutputResults = new PHprCommandResult?[EffectCount];
    private readonly PHprSafetyDecision?[] _lastSafetyDecisions = new PHprSafetyDecision?[EffectCount];
    private readonly PHprSafetyViolation?[] _lastSafetyViolations = new PHprSafetyViolation?[EffectCount];
    private readonly DateTimeOffset?[] _lastRouteAtUtc = new DateTimeOffset?[EffectCount];
    private readonly DateTimeOffset?[,] _lastAttemptAtUtc = new DateTimeOffset?[EffectCount, ModuleCount];
    private PHprPedalEffectsRouterOptions _options;
    private long _evaluationCount;
    private long _ignoredEvaluationCount;
    private PHprPedalEffectKind? _lastActiveEffect;
    private PHprGearPulseTarget? _lastTargetModule;
    private PHprCommand? _lastCommand;
    private PHprCommandResult? _lastOutputResult;
    private PHprPedalEffectsRoutingResult? _lastResult;
    private string? _lastError;

    private const int EffectCount = 3;
    private const int ModuleCount = 2;

    public PHprPedalEffectsRouter(
        SafetyLimitedPhprOutputDevice output,
        PHprPedalEffectsRouterOptions? options = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _options = (options ?? PHprPedalEffectsRouterOptions.Default).Normalize();
    }

    public void Configure(PHprPedalEffectsRouterOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PHprPedalEffectsRouterOptions.Default).Normalize();
        }
    }

    public PHprPedalEffectsRoutingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var outputSnapshot = _output.GetSnapshot();
            var safetySnapshot = _output.SafetySnapshot;
            return new PHprPedalEffectsRoutingSnapshot(
                _options,
                _evaluationCount,
                _ignoredEvaluationCount,
                CreateDiagnosticsLocked(PHprPedalEffectKind.RoadVibration),
                CreateDiagnosticsLocked(PHprPedalEffectKind.WheelSlip),
                CreateDiagnosticsLocked(PHprPedalEffectKind.WheelLock),
                _lastActiveEffect,
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

    public ValueTask<PHprPedalEffectsRoutingResult> RouteAsync(
        HapticPipelineSnapshot? pipelineSnapshot,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pipelineSnapshot is null)
        {
            return ValueTask.FromResult(StoreIgnored(
                PHprPedalEffectsRoutingStatus.IgnoredMissingVehicleState,
                "No HapticPipelineSnapshot was supplied; no mock P-HPR pedal effect command was sent.",
                nowUtc ?? DateTimeOffset.UtcNow));
        }

        var context = safetyContext ?? BuildContext(pipelineSnapshot);
        return pipelineSnapshot.HapticFrame is not null
            ? RouteAsync(pipelineSnapshot.HapticFrame, pipelineSnapshot.VehicleState, context, nowUtc, cancellationToken)
            : RouteAsync(pipelineSnapshot.VehicleState, context, nowUtc, cancellationToken);
    }

    public async ValueTask<PHprPedalEffectsRoutingResult> RouteAsync(
        VehicleState? vehicleState,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PHprPedalEffectsRouterOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (!options.IsEnabled)
        {
            return StoreIgnored(
                PHprPedalEffectsRoutingStatus.IgnoredDisabled,
                "Mock P-HPR pedal effects routing is disabled; no command was sent.",
                now);
        }

        if (vehicleState is null)
        {
            return StoreIgnored(
                PHprPedalEffectsRoutingStatus.IgnoredMissingVehicleState,
                "No VehicleState was supplied; no mock P-HPR pedal effect command was sent.",
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
                        ? PHprPedalEffectsRoutingStatus.IgnoredMinimumInterval
                        : PHprPedalEffectsRoutingStatus.IgnoredNoActiveEffect,
                    anyActive
                        ? "Mock P-HPR pedal effects were active, but the deterministic minimum route interval suppressed this update."
                        : "No road vibration, wheel slip, or wheel lock pedal effect was active; no command was sent.",
                    now);
            }

            var context = BuildContext(safetyContext);
            _output.SetSafetyContext(context);

            var commandResults = new List<PHprPedalEffectRoutingCommandResult>(plans.Count);
            foreach (var plan in plans)
            {
                var command = PHprCommand.Create(
                    plan.TargetModule.ToModuleId(),
                    plan.Strength01,
                    plan.FrequencyHz,
                    plan.DurationMs,
                    plan.Kind.ToCommandSource(),
                    plan.Priority,
                    now,
                    PHprSafetyFlags.MockOnly);
                var outputResult = await _output.SendAsync(command, cancellationToken).ConfigureAwait(false);
                commandResults.Add(new PHprPedalEffectRoutingCommandResult(
                    plan.Kind,
                    plan.TargetModule,
                    outputResult.Command ?? command,
                    outputResult));
                StoreCommandResult(plan, outputResult, now);
            }

            var outputSnapshot = _output.GetSnapshot();
            var safetySnapshot = _output.SafetySnapshot;
            var routedCount = commandResults.Count(result => result.WasRouted);
            var resultStatus = routedCount > 0
                ? PHprPedalEffectsRoutingStatus.Routed
                : PHprPedalEffectsRoutingStatus.RejectedBySafety;
            var result = new PHprPedalEffectsRoutingResult(
                resultStatus,
                routedCount > 0
                    ? $"Mock P-HPR pedal effects routed {routedCount:N0} command(s) through the safety-limited mock output; no hardware write was performed."
                    : "Mock P-HPR pedal effects were rejected by the safety-limited mock output; no hardware write was performed.",
                commandResults,
                safetySnapshot,
                outputSnapshot,
                now);

            StoreCompleted(result, plans);
            return result;
        }
        catch (Exception ex)
        {
            var result = new PHprPedalEffectsRoutingResult(
                PHprPedalEffectsRoutingStatus.Failed,
                $"Mock P-HPR pedal effects routing failed: {ex.Message}",
                [],
                _output.SafetySnapshot,
                _output.GetSnapshot(),
                now);
            StoreFailed(result, ex.Message);
            return result;
        }
    }

    internal async ValueTask<PHprPedalEffectsRoutingResult> RouteAsync(
        HapticFrame frame,
        VehicleState vehicleState,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(vehicleState);

        cancellationToken.ThrowIfCancellationRequested();

        PHprPedalEffectsRouterOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (!options.IsEnabled)
        {
            return StoreIgnored(
                PHprPedalEffectsRoutingStatus.IgnoredDisabled,
                "Mock P-HPR pedal effects routing is disabled; no command was sent.",
                now);
        }

        try
        {
            var candidates = EvaluateEffects(frame, vehicleState, options);
            StoreEvaluation(candidates);

            var plans = CreateRoutePlans(candidates, options, now);
            if (plans.Count == 0)
            {
                var anyActive = candidates.Any(candidate => candidate.IsActive);
                return StoreIgnored(
                    anyActive
                        ? PHprPedalEffectsRoutingStatus.IgnoredMinimumInterval
                        : PHprPedalEffectsRoutingStatus.IgnoredNoActiveEffect,
                    anyActive
                        ? "Mock P-HPR pedal effects were active, but the deterministic minimum route interval suppressed this update."
                        : "No road vibration, wheel slip, or wheel lock pedal effect was active; no command was sent.",
                    now);
            }

            var context = BuildContext(safetyContext);
            _output.SetSafetyContext(context);

            var commandResults = new List<PHprPedalEffectRoutingCommandResult>(plans.Count);
            foreach (var plan in plans)
            {
                var command = PHprCommand.Create(
                    plan.TargetModule.ToModuleId(),
                    plan.Strength01,
                    plan.FrequencyHz,
                    plan.DurationMs,
                    plan.Kind.ToCommandSource(),
                    plan.Priority,
                    now,
                    PHprSafetyFlags.MockOnly);
                var outputResult = await _output.SendAsync(command, cancellationToken).ConfigureAwait(false);
                commandResults.Add(new PHprPedalEffectRoutingCommandResult(
                    plan.Kind,
                    plan.TargetModule,
                    outputResult.Command ?? command,
                    outputResult));
                StoreCommandResult(plan, outputResult, now);
            }

            var outputSnapshot = _output.GetSnapshot();
            var safetySnapshot = _output.SafetySnapshot;
            var routedCount = commandResults.Count(result => result.WasRouted);
            var resultStatus = routedCount > 0
                ? PHprPedalEffectsRoutingStatus.Routed
                : PHprPedalEffectsRoutingStatus.RejectedBySafety;
            var result = new PHprPedalEffectsRoutingResult(
                resultStatus,
                routedCount > 0
                    ? $"Mock P-HPR pedal effects routed {routedCount:N0} command(s) through the safety-limited mock output; no hardware write was performed."
                    : "Mock P-HPR pedal effects were rejected by the safety-limited mock output; no hardware write was performed.",
                commandResults,
                safetySnapshot,
                outputSnapshot,
                now);

            StoreCompleted(result, plans);
            return result;
        }
        catch (Exception ex)
        {
            var result = new PHprPedalEffectsRoutingResult(
                PHprPedalEffectsRoutingStatus.Failed,
                $"Mock P-HPR pedal effects routing failed: {ex.Message}",
                [],
                _output.SafetySnapshot,
                _output.GetSnapshot(),
                now);
            StoreFailed(result, ex.Message);
            return result;
        }
    }

    public async ValueTask<PHprPedalEffectsRoutingResult> EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _output.EmergencyStopAsync(cancellationToken).ConfigureAwait(false);
        var outputSnapshot = _output.GetSnapshot();
        var safetySnapshot = _output.SafetySnapshot;
        var result = new PHprPedalEffectsRoutingResult(
            PHprPedalEffectsRoutingStatus.EmergencyStopped,
            "Mock P-HPR pedal-effects emergency stop activated through the safety-limited output; no hardware write was performed.",
            [],
            safetySnapshot,
            outputSnapshot,
            DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _lastCommand = outputSnapshot.LastCommand;
            _lastOutputResult = outputSnapshot.LastCommand is null
                ? null
                : PHprCommandResult.Accepted(outputSnapshot.LastCommand, "Mock emergency stop recorded.");
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
            Array.Clear(_routeCounts);
            Array.Clear(_safetyRejectedCounts);
            Array.Clear(_intervalSuppressedCounts);
            Array.Clear(_lastActive);
            Array.Clear(_lastIntensity);
            Array.Clear(_lastTargets);
            Array.Clear(_lastCommands);
            Array.Clear(_lastOutputResults);
            Array.Clear(_lastSafetyDecisions);
            Array.Clear(_lastSafetyViolations);
            Array.Clear(_lastRouteAtUtc);
            Array.Clear(_lastAttemptAtUtc);
            _evaluationCount = 0;
            _ignoredEvaluationCount = 0;
            _lastActiveEffect = null;
            _lastTargetModule = null;
            _lastCommand = null;
            _lastOutputResult = null;
            _lastResult = null;
            _lastError = null;
        }

        _output.Inner.ClearHistory();
        _output.ResetSafetyState();
    }

    private static PHprSafetyContext BuildContext(HapticPipelineSnapshot snapshot)
    {
        var context = PHprSafetyContext.DefaultMock with
        {
            TelemetryStale = snapshot.TelemetryTimedOutMuted,
            HapticsStopped = !snapshot.IsRunning,
            EmergencyMuteActive = snapshot.EmergencyMute
        };

        return BuildContext(context);
    }

    private static PHprSafetyContext BuildContext(PHprSafetyContext? safetyContext)
    {
        var context = safetyContext ?? PHprSafetyContext.DefaultMock;
        return context with
        {
            IsMockOutput = true,
            RequiresRealDeviceWrites = false,
            SoftwareConflictStatus = context.SoftwareConflictStatus == PHprSoftwareConflictStatus.Unknown
                ? PHprSoftwareConflictStatus.Clear
                : context.SoftwareConflictStatus
        };
    }

    private PHprPedalEffectDiagnostics CreateDiagnosticsLocked(PHprPedalEffectKind kind)
    {
        var index = ToIndex(kind);
        return new PHprPedalEffectDiagnostics(
            kind,
            _options.GetState(kind),
            _lastActive[index],
            _lastIntensity[index],
            _lastTargets[index],
            kind.ToCommandSource(),
            _routeCounts[index],
            _safetyRejectedCounts[index],
            _intervalSuppressedCounts[index],
            _lastCommands[index],
            _lastOutputResults[index],
            _lastSafetyDecisions[index],
            _lastSafetyViolations[index],
            _lastRouteAtUtc[index]);
    }

    private IReadOnlyList<EffectCandidate> EvaluateEffects(
        VehicleState vehicleState,
        PHprPedalEffectsRouterOptions options)
    {
        var frame = LegacyActuationHapticFrameFactory.FromVehicleState(vehicleState);
        return EvaluateEffects(frame, vehicleState, options);
    }

    private IReadOnlyList<EffectCandidate> EvaluateEffects(
        HapticFrame frame,
        VehicleState vehicleState,
        PHprPedalEffectsRouterOptions options)
    {
        var slipLockEvaluation = _slipLockEvaluator.Evaluate(
            SlipLockEvaluationInput.FromHapticFrame(
                frame,
                vehicleState,
                _slipLockEvaluator.Options,
                options.WheelSlip.IsEnabled,
                options.WheelLock.IsEnabled));
        return
        [
            EvaluateRoadVibration(frame, options.RoadVibration),
            EvaluateWheelSlip(options.WheelSlip, slipLockEvaluation),
            EvaluateWheelLock(options.WheelLock, slipLockEvaluation)
        ];
    }

    private static EffectCandidate EvaluateRoadVibration(
        HapticFrame frame,
        PHprPedalEffectState state)
    {
        if (!state.IsEnabled
            || !frame.Context.AllowsDrivingOutput
            || frame.Signals.SurfaceKinds is null
            || !frame.Freshness.TryGetValue(HapticFrameSignalNames.Telemetry, out var telemetryFreshness)
            || !telemetryFreshness.IsFresh
            || frame.Signals.SpeedMetersPerSecond is null)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.RoadVibration, state);
        }

        var speedScale = SpeedScale(frame.Signals.SpeedMetersPerSecond.Value * 3.6f, 5f, 160f);
        if (speedScale <= 0f)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.RoadVibration, state);
        }

        var surfaceKinds = frame.Signals.SurfaceKinds;
        var surfaceMix = 0f;
        surfaceMix += SurfaceGain(surfaceKinds.RearLeft);
        surfaceMix += SurfaceGain(surfaceKinds.RearRight);
        surfaceMix += SurfaceGain(surfaceKinds.FrontLeft);
        surfaceMix += SurfaceGain(surfaceKinds.FrontRight);

        surfaceMix = Clamp(surfaceMix / 4f, 0f, 1f);
        var intensity = Clamp(speedScale * surfaceMix, 0f, 1f);
        return intensity <= 0f
            ? EffectCandidate.Inactive(PHprPedalEffectKind.RoadVibration, state)
            : new EffectCandidate(PHprPedalEffectKind.RoadVibration, state, true, intensity);
    }

    private static EffectCandidate EvaluateWheelSlip(
        PHprPedalEffectState state,
        SlipLockEvaluationResult evaluation)
    {
        if (!state.IsEnabled
            || evaluation.DrivingStateMuted
            || !evaluation.TelemetryFresh
            || !evaluation.MotionExFresh)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, state);
        }

        if (evaluation.WheelSlip.SuppressionReason == SlipLockSuppressionReason.BelowMinimumSpeed)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, state);
        }

        var intensity = evaluation.WheelSlip.Intensity01;
        return intensity <= 0f
            ? EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, state)
            : new EffectCandidate(PHprPedalEffectKind.WheelSlip, state, true, intensity);
    }

    private static EffectCandidate EvaluateWheelLock(
        PHprPedalEffectState state,
        SlipLockEvaluationResult evaluation)
    {
        if (!state.IsEnabled
            || evaluation.DrivingStateMuted
            || !evaluation.TelemetryFresh
            || !evaluation.MotionExFresh)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, state);
        }

        if (evaluation.WheelLock.SuppressionReason == SlipLockSuppressionReason.BelowBrakeThreshold)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, state);
        }

        if (evaluation.WheelLock.SuppressionReason == SlipLockSuppressionReason.BelowMinimumSpeed)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, state);
        }

        var intensity = evaluation.WheelLock.Intensity01;
        return intensity <= 0f
            ? EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, state)
            : new EffectCandidate(PHprPedalEffectKind.WheelLock, state, true, intensity);
    }

    private List<RoutePlan> CreateRoutePlans(
        IReadOnlyList<EffectCandidate> candidates,
        PHprPedalEffectsRouterOptions options,
        DateTimeOffset nowUtc)
    {
        var plans = new List<RoutePlan>(capacity: 3);
        var claimedBrake = false;
        var claimedThrottle = false;

        foreach (var candidate in candidates
            .Where(candidate => candidate.IsActive)
            .OrderByDescending(candidate => candidate.State.Profile.Priority))
        {
            var modules = ExpandTarget(candidate.State.TargetModule);
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
            var profile = candidate.State.Profile;
            plans.Add(new RoutePlan(
                candidate.Kind,
                target,
                profile.ScaleStrength(candidate.Intensity01),
                profile.ScaleFrequency(candidate.Intensity01),
                profile.DurationMs,
                profile.Priority,
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
        var safetySnapshot = _output.SafetySnapshot;
        lock (_gate)
        {
            foreach (var module in plan.Modules)
            {
                _lastAttemptAtUtc[index, ToModuleIndex(module)] = nowUtc;
            }

            _lastTargets[index] = plan.TargetModule;
            _lastCommands[index] = outputResult.Command;
            _lastOutputResults[index] = outputResult;
            _lastSafetyDecisions[index] = safetySnapshot.LastDecision;
            _lastSafetyViolations[index] = safetySnapshot.LastViolation;

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
        PHprPedalEffectsRoutingResult result,
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

    private PHprPedalEffectsRoutingResult StoreIgnored(
        PHprPedalEffectsRoutingStatus status,
        string message,
        DateTimeOffset nowUtc)
    {
        var result = new PHprPedalEffectsRoutingResult(
            status,
            message,
            [],
            _output.SafetySnapshot,
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

    private void StoreFailed(PHprPedalEffectsRoutingResult result, string errorMessage)
    {
        lock (_gate)
        {
            _ignoredEvaluationCount++;
            _lastResult = result;
            _lastError = string.IsNullOrWhiteSpace(errorMessage)
                ? "Unknown mock pedal effects routing error."
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

    private static float SurfaceGain(SurfaceKind surfaceKind)
    {
        return surfaceKind switch
        {
            SurfaceKind.Tarmac => 0.18f,
            SurfaceKind.RumbleStrip => 0.90f,
            SurfaceKind.Concrete => 0.28f,
            SurfaceKind.Rock => 0.55f,
            SurfaceKind.Gravel => 0.65f,
            SurfaceKind.Mud => 0.38f,
            SurfaceKind.Sand => 0.32f,
            SurfaceKind.Grass => 0.42f,
            SurfaceKind.Water => 0.20f,
            SurfaceKind.Cobblestone => 0.70f,
            SurfaceKind.Metal => 0.50f,
            SurfaceKind.Ridged => 0.85f,
            _ => 0.25f
        };
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
        return kind switch
        {
            PHprPedalEffectKind.RoadVibration => 0,
            PHprPedalEffectKind.WheelSlip => 1,
            PHprPedalEffectKind.WheelLock => 2,
            _ => 0
        };
    }

    private static int ToModuleIndex(PHprModuleId module)
    {
        return module == PHprModuleId.Throttle ? 1 : 0;
    }

    private sealed record EffectCandidate(
        PHprPedalEffectKind Kind,
        PHprPedalEffectState State,
        bool IsActive,
        double Intensity01)
    {
        public static EffectCandidate Inactive(PHprPedalEffectKind kind, PHprPedalEffectState state)
        {
            return new EffectCandidate(kind, state, false, 0d);
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
