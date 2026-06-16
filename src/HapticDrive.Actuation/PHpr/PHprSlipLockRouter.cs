using HapticDrive.Asio.Core.Haptics;
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
    private const int GearProtectionWindowMs = 150;
    private const double TactileThresholdStrength01 = 0.05d;
    private readonly object _gate = new();
    private readonly IPHprOutputDevice _output;
    private readonly Action<PHprSafetyContext>? _applySafetyContext;
    private readonly SlipLockEvaluator _slipLockEvaluator = new();
    private readonly long[] _routeCounts = new long[EffectCount];
    private readonly long[] _safetyRejectedCounts = new long[EffectCount];
    private readonly long[] _intervalSuppressedCounts = new long[EffectCount];
    private readonly long[] _staleTelemetrySuppressedCounts = new long[EffectCount];
    private readonly long[] _commandRateSuppressedCounts = new long[EffectCount];
    private readonly long[] _stopCommandCounts = new long[EffectCount];
    private readonly bool[] _lastActive = new bool[EffectCount];
    private readonly string[] _lastReasons = new string[EffectCount];
    private readonly double[] _lastIntensity = new double[EffectCount];
    private readonly double[] _lastStrength = new double[EffectCount];
    private readonly double[] _lastFrequency = new double[EffectCount];
    private readonly int[] _lastDurationMs = new int[EffectCount];
    private readonly bool[] _lastBelowTactileThreshold = new bool[EffectCount];
    private readonly PHprSlipLockTelemetrySnapshot?[] _lastTelemetry = new PHprSlipLockTelemetrySnapshot?[EffectCount];
    private readonly PHprGearPulseTarget?[] _lastTargets = new PHprGearPulseTarget?[EffectCount];
    private readonly PHprCommand?[] _lastCommands = new PHprCommand?[EffectCount];
    private readonly PHprCommandResult?[] _lastOutputResults = new PHprCommandResult?[EffectCount];
    private readonly DateTimeOffset?[] _lastRouteAtUtc = new DateTimeOffset?[EffectCount];
    private readonly DateTimeOffset?[] _lastStartAtUtc = new DateTimeOffset?[EffectCount];
    private readonly DateTimeOffset?[] _lastUpdateAtUtc = new DateTimeOffset?[EffectCount];
    private readonly DateTimeOffset?[] _lastStopAtUtc = new DateTimeOffset?[EffectCount];
    private readonly DateTimeOffset?[,] _lastAttemptAtUtc = new DateTimeOffset?[EffectCount, ModuleCount];
    private readonly PHprPedalEffectKind?[] _activeModuleOwners = new PHprPedalEffectKind?[ModuleCount];
    private PHprSlipLockRouterOptions _options;
    private DateTimeOffset? _lastGearPulseAtUtc;
    private DateTimeOffset? _firstRouteAttemptAtUtc;
    private DateTimeOffset? _lastRouteAttemptAtUtc;
    private DateTimeOffset? _lastCommandRoutedAtUtc;
    private long _routeAttemptCount;
    private long _evaluationCount;
    private long _ignoredEvaluationCount;
    private long _routeCount;
    private long _safetyRejectedCount;
    private long _intervalSuppressedCount;
    private long _staleTelemetrySuppressedCount;
    private long _commandRateSuppressedCount;
    private long _stopCommandCount;
    private long _gearProtectionSuppressedCount;
    private long _watchdogStopCount;
    private PHprPedalEffectKind? _lastActiveEffect;
    private PHprGearPulseTarget? _lastTargetModule;
    private PHprCommand? _lastCommand;
    private PHprCommandResult? _lastOutputResult;
    private PHprSlipLockRoutingResult? _lastResult;
    private string _runtimeState = "Idle";
    private string _lastSlipLockStopReason = "none";
    private string? _lastIgnoredReason;
    private string? _lastError;

    public PHprSlipLockRouter(
        IPHprOutputDevice output,
        PHprSlipLockRouterOptions? options = null,
        Action<PHprSafetyContext>? applySafetyContext = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _options = (options ?? PHprSlipLockRouterOptions.Disabled).Normalize();
        _applySafetyContext = applySafetyContext;
        _lastReasons[ToIndex(PHprPedalEffectKind.WheelSlip)] = "not evaluated";
        _lastReasons[ToIndex(PHprPedalEffectKind.WheelLock)] = "not evaluated";
    }

    public void Configure(PHprSlipLockRouterOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PHprSlipLockRouterOptions.Disabled).Normalize();
        }
    }

    public void NotifyGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        lock (_gate)
        {
            _lastGearPulseAtUtc = timestampUtc ?? DateTimeOffset.UtcNow;
        }
    }

    public PHprSlipLockRoutingSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new PHprSlipLockRoutingSnapshot(
                _options,
                _routeAttemptCount,
                _evaluationCount,
                _ignoredEvaluationCount,
                _routeCount,
                _safetyRejectedCount,
                _intervalSuppressedCount,
                _staleTelemetrySuppressedCount,
                _commandRateSuppressedCount,
                _stopCommandCount,
                CreateDiagnosticsLocked(PHprPedalEffectKind.WheelSlip),
                CreateDiagnosticsLocked(PHprPedalEffectKind.WheelLock),
                _lastActiveEffect,
                _lastTargetModule,
                _lastCommand,
                _lastOutputResult,
                _lastResult,
                _output.GetSnapshot(),
                _firstRouteAttemptAtUtc,
                _lastRouteAttemptAtUtc,
                _lastCommandRoutedAtUtc,
                _runtimeState,
                FormatActiveModulesLocked(),
                MaxTimestamp(_lastStartAtUtc),
                MaxTimestamp(_lastUpdateAtUtc),
                MaxTimestamp(_lastStopAtUtc),
                _lastSlipLockStopReason,
                _gearProtectionSuppressedCount,
                _watchdogStopCount,
                _lastIgnoredReason,
                _lastError);
        }
    }

    public ValueTask StopAsync(
        string reason,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        return StopOwnedModulesAsync(
            string.IsNullOrWhiteSpace(reason) ? "P-HPR slip/lock stop requested." : reason.Trim(),
            now,
            countAsWatchdog: false,
            cancellationToken);
    }

    public async ValueTask StopIfHoldExpiredAsync(
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        PHprSlipLockRouterOptions options;
        DateTimeOffset? lastUpdate;
        bool active;
        lock (_gate)
        {
            options = _options.Normalize();
            lastUpdate = MaxTimestamp(_lastUpdateAtUtc);
            active = HasActiveModulesLocked();
        }

        if (!active || lastUpdate is null || options.HoldTimeout <= TimeSpan.Zero)
        {
            return;
        }

        if (now - lastUpdate.Value >= options.HoldTimeout)
        {
            await StopOwnedModulesAsync(
                "P-HPR slip/lock watchdog stopped output because updates exceeded hold timeout.",
                now,
                countAsWatchdog: true,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<PHprSlipLockRoutingResult> RouteAsync(
        HapticPipelineSnapshot? pipelineSnapshot,
        PHprSafetyContext? safetyContext = null,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (pipelineSnapshot is null)
        {
            var now = nowUtc ?? DateTimeOffset.UtcNow;
            StoreRouteAttempt(now);
            await StopOwnedModulesAsync(
                "P-HPR slip/lock stopped because no HapticPipelineSnapshot was supplied.",
                now,
                countAsWatchdog: false,
                cancellationToken).ConfigureAwait(false);
            return StoreIgnored(
                PHprSlipLockRoutingStatus.IgnoredMissingVehicleState,
                "No HapticPipelineSnapshot was supplied; no P-HPR slip/lock command was sent.",
                now);
        }

        var context = safetyContext ?? BuildContext(pipelineSnapshot);
        return await RouteAsync(pipelineSnapshot.VehicleState, context, nowUtc, cancellationToken).ConfigureAwait(false);
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
        StoreRouteAttempt(now);
        if (!options.IsEnabled)
        {
            await StopOwnedModulesAsync(
                "P-HPR slip/lock routing was disabled.",
                now,
                countAsWatchdog: false,
                cancellationToken).ConfigureAwait(false);
            return StoreIgnored(
                PHprSlipLockRoutingStatus.IgnoredDisabled,
                "P-HPR slip/lock routing is disabled; no command was sent.",
                now);
        }

        if (vehicleState is null)
        {
            await StopOwnedModulesAsync(
                "P-HPR slip/lock stopped because no VehicleState was supplied.",
                now,
                countAsWatchdog: false,
                cancellationToken).ConfigureAwait(false);
            return StoreIgnored(
                PHprSlipLockRoutingStatus.IgnoredMissingVehicleState,
                "No VehicleState was supplied; no P-HPR slip/lock command was sent.",
                now);
        }

        try
        {
            var context = safetyContext ?? PHprSafetyContext.DefaultMock;
            if (context.HapticsStopped)
            {
                await StopOwnedModulesAsync(
                    "P-HPR slip/lock stopped because haptics are stopped.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    "P-HPR slip/lock was dropped because haptics are stopped.",
                    now);
            }

            if (context.TelemetryStale)
            {
                IncrementStaleTelemetrySuppressed(PHprPedalEffectKind.WheelSlip);
                IncrementStaleTelemetrySuppressed(PHprPedalEffectKind.WheelLock);
                await StopOwnedModulesAsync(
                    "P-HPR slip/lock stopped because telemetry is stale.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    "P-HPR slip/lock was dropped because telemetry is stale.",
                    now);
            }

            if (!context.DrivingArmed)
            {
                await StopOwnedModulesAsync(
                    "P-HPR slip/lock stopped because DrivingArmed is false.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    "P-HPR slip/lock was dropped because DrivingArmed is false.",
                    now);
            }

            if (context.EmergencyMuteActive || context.EmergencyStopActive)
            {
                await StopOwnedModulesAsync(
                    "P-HPR slip/lock stopped because emergency protection is active.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    "P-HPR slip/lock was dropped because emergency protection is active.",
                    now);
            }

            if (context.SoftwareConflictStatus == PHprSoftwareConflictStatus.ActiveConflict)
            {
                await StopOwnedModulesAsync(
                    "P-HPR slip/lock stopped because direct-control coexistence is blocked.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    "P-HPR slip/lock was dropped because SimPro/SimHub coexistence is blocked.",
                    now);
            }

            if (IsGearProtectionActive(now))
            {
                IncrementGearProtectionSuppressed();
                await StopOwnedModulesAsync(
                    "P-HPR slip/lock stopped because a recent gear pulse is inside the protection window.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    "P-HPR slip/lock was suppressed because a recent gear pulse is inside the protection window.",
                    now);
            }

            var candidates = EvaluateEffects(vehicleState, options);
            StoreEvaluation(candidates);

            if (candidates.All(candidate => !candidate.IsActive))
            {
                foreach (var candidate in candidates.Where(candidate => candidate.IsTelemetryStale))
                {
                    IncrementStaleTelemetrySuppressed(candidate.Kind);
                }

                await StopOwnedModulesAsync(
                    $"P-HPR slip/lock stopped because no active effect was detected: {string.Join("; ", candidates.Select(candidate => $"{candidate.Kind}: {candidate.Reason}"))}.",
                    now,
                    countAsWatchdog: false,
                    cancellationToken).ConfigureAwait(false);
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredNoActiveEffect,
                    $"No wheel slip or wheel lock effect was active; no P-HPR command was sent. Reasons: {string.Join("; ", candidates.Select(candidate => $"{candidate.Kind}: {candidate.Reason}"))}.",
                    now,
                    candidates.Max(candidate => candidate.Intensity01));
            }

            var planning = CreateRoutePlans(candidates, options, now);
            if (planning.Plans.Count == 0)
            {
                return StoreIgnored(
                    PHprSlipLockRoutingStatus.IgnoredMinimumInterval,
                    "P-HPR slip/lock effects were active, but the deterministic minimum route interval suppressed this update.",
                    now,
                    candidates.Max(candidate => candidate.Intensity01));
            }

            _applySafetyContext?.Invoke(context);

            var commandResults = new List<PHprSlipLockRoutingCommandResult>(planning.Plans.Count);
            foreach (var plan in planning.Plans)
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

            await StopInactiveOrPreemptedEffectsAsync(
                candidates,
                planning.PreemptedKinds,
                now,
                cancellationToken).ConfigureAwait(false);

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
                now,
                planning.MaximumIntensity01);

            StoreCompleted(result, planning.Plans);
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
            _lastReasons[index],
            _lastIntensity[index],
            _lastStrength[index],
            _lastFrequency[index],
            _lastDurationMs[index],
            _lastBelowTactileThreshold[index],
            _lastTelemetry[index],
            _lastTargets[index],
            _routeCounts[index],
            _safetyRejectedCounts[index],
            _intervalSuppressedCounts[index],
            _staleTelemetrySuppressedCounts[index],
            _commandRateSuppressedCounts[index],
            _stopCommandCounts[index],
            _lastCommands[index],
            _lastOutputResults[index],
            _lastRouteAtUtc[index],
            _lastStartAtUtc[index],
            _lastUpdateAtUtc[index],
            _lastStopAtUtc[index]);
    }

    private IReadOnlyList<EffectCandidate> EvaluateEffects(
        VehicleState vehicleState,
        PHprSlipLockRouterOptions options)
    {
        var evaluation = _slipLockEvaluator.Evaluate(
            SlipLockEvaluationInput.FromVehicleState(
                vehicleState,
                _slipLockEvaluator.Options,
                options.WheelSlip.IsEnabled,
                options.WheelLock.IsEnabled));
        return
        [
            EvaluateWheelSlip(options.WheelSlip, evaluation),
            EvaluateWheelLock(options.WheelLock, evaluation)
        ];
    }

    private static EffectCandidate EvaluateWheelSlip(
        PHprSlipLockEffectSettings settings,
        SlipLockEvaluationResult evaluation)
    {
        settings = settings.Normalize(PHprPedalEffectKind.WheelSlip);
        var telemetry = BuildTelemetrySnapshot(evaluation);

        if (!settings.IsEnabled)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings, "disabled in settings", telemetry);
        }

        if (evaluation.DrivingStateMuted)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings, "driving state muted", telemetry);
        }

        if (!evaluation.TelemetryFresh)
        {
            return EffectCandidate.Stale(PHprPedalEffectKind.WheelSlip, settings, "telemetry sample is stale", telemetry);
        }

        if (!evaluation.MotionExFresh)
        {
            return EffectCandidate.Stale(PHprPedalEffectKind.WheelSlip, settings, "wheel-motion sample is stale", telemetry);
        }

        if (evaluation.WheelSlip.SuppressionReason == SlipLockSuppressionReason.BelowMinimumSpeed)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings, "below 8 km/h activation threshold", telemetry);
        }

        var intensity = evaluation.WheelSlip.Intensity01;
        if (intensity <= 0f)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelSlip, settings, "below slip ratio/angle threshold", telemetry);
        }

        return EffectCandidate.Active(
            PHprPedalEffectKind.WheelSlip,
            settings,
            intensity,
            evaluation.WheelSlip.IsAssistedAttenuated
                ? "active from wheel slip ratio/angle; traction control attenuated"
                : "active from wheel slip ratio/angle",
            telemetry);
    }

    private static EffectCandidate EvaluateWheelLock(
        PHprSlipLockEffectSettings settings,
        SlipLockEvaluationResult evaluation)
    {
        settings = settings.Normalize(PHprPedalEffectKind.WheelLock);
        var telemetry = BuildTelemetrySnapshot(evaluation);

        if (!settings.IsEnabled)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings, "disabled in settings", telemetry);
        }

        if (evaluation.DrivingStateMuted)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings, "driving state muted", telemetry);
        }

        if (!evaluation.TelemetryFresh)
        {
            return EffectCandidate.Stale(PHprPedalEffectKind.WheelLock, settings, "telemetry sample is stale", telemetry);
        }

        if (!evaluation.MotionExFresh)
        {
            return EffectCandidate.Stale(PHprPedalEffectKind.WheelLock, settings, "wheel-motion sample is stale", telemetry);
        }

        if (evaluation.WheelLock.SuppressionReason == SlipLockSuppressionReason.BelowBrakeThreshold)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings, "brake input below 10%", telemetry);
        }

        if (evaluation.WheelLock.SuppressionReason == SlipLockSuppressionReason.BelowMinimumSpeed)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings, "below 8 km/h activation threshold", telemetry);
        }

        var intensity = evaluation.WheelLock.Intensity01;
        if (intensity <= 0f)
        {
            return EffectCandidate.Inactive(PHprPedalEffectKind.WheelLock, settings, "below lock threshold", telemetry);
        }

        return EffectCandidate.Active(
            PHprPedalEffectKind.WheelLock,
            settings,
            intensity,
            evaluation.WheelLock.IsAssistedAttenuated
                ? "active from low wheel speed under braking; ABS attenuated"
                : "active from low wheel speed under braking",
            telemetry);
    }

    private RoutePlanningResult CreateRoutePlans(
        IReadOnlyList<EffectCandidate> candidates,
        PHprSlipLockRouterOptions options,
        DateTimeOffset nowUtc)
    {
        var plans = new List<RoutePlan>(capacity: 2);
        var preemptedKinds = new HashSet<PHprPedalEffectKind>();
        var claimedBrake = false;
        var claimedThrottle = false;

        foreach (var candidate in candidates
            .Where(candidate => candidate.IsActive)
            .OrderByDescending(candidate => candidate.Settings.Priority))
        {
            var modules = ExpandTarget(candidate.Settings.TargetModule);
            var allowedModules = new List<PHprModuleId>(capacity: 2);
            var blockedByClaim = false;
            var intervalSuppressed = false;
            foreach (var module in modules)
            {
                if ((module == PHprModuleId.Brake && claimedBrake)
                    || (module == PHprModuleId.Throttle && claimedThrottle))
                {
                    blockedByClaim = true;
                    continue;
                }

                if (IsWithinMinimumInterval(candidate.Kind, module, options.MinimumRouteInterval, nowUtc))
                {
                    intervalSuppressed = true;
                    IncrementIntervalSuppressed(candidate.Kind);
                    continue;
                }

                allowedModules.Add(module);
            }

            if (allowedModules.Count == 0)
            {
                if (blockedByClaim && !intervalSuppressed)
                {
                    preemptedKinds.Add(candidate.Kind);
                }

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

            plans.Add(new RoutePlan(
                candidate.Kind,
                ToTarget(allowedModules),
                candidate.ComputedStrength01,
                candidate.ComputedFrequencyHz,
                candidate.DurationMs,
                candidate.Settings.Priority,
                allowedModules,
                candidate.Intensity01));
        }

        return new RoutePlanningResult(plans, preemptedKinds, candidates.Max(candidate => candidate.Intensity01));
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
                _lastReasons[index] = candidate.Reason;
                _lastIntensity[index] = candidate.Intensity01;
                _lastStrength[index] = candidate.ComputedStrength01;
                _lastFrequency[index] = candidate.ComputedFrequencyHz;
                _lastDurationMs[index] = candidate.DurationMs;
                _lastBelowTactileThreshold[index] = candidate.BelowTactileThreshold;
                _lastTelemetry[index] = candidate.Telemetry;
            }
        }
    }

    private void StoreRouteAttempt(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            _routeAttemptCount++;
            _firstRouteAttemptAtUtc ??= nowUtc;
            _lastRouteAttemptAtUtc = nowUtc;
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
            _lastCommand = outputResult.Command;
            _lastOutputResult = outputResult;
            _lastTargetModule = plan.TargetModule;

            if (outputResult.Succeeded)
            {
                _routeCounts[index]++;
                _routeCount++;
                _lastRouteAtUtc[index] = nowUtc;
                _lastCommandRoutedAtUtc = nowUtc;
                MarkEffectActiveLocked(plan.Kind, plan.Modules, nowUtc);
                return;
            }

            _safetyRejectedCounts[index]++;
            _safetyRejectedCount++;
            if (IsCommandRateSuppression(outputResult))
            {
                _commandRateSuppressedCounts[index]++;
                _commandRateSuppressedCount++;
            }
        }
    }

    private async ValueTask StopInactiveOrPreemptedEffectsAsync(
        IReadOnlyList<EffectCandidate> candidates,
        IReadOnlySet<PHprPedalEffectKind> preemptedKinds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in candidates)
        {
            if (!candidate.IsActive || preemptedKinds.Contains(candidate.Kind))
            {
                await StopOwnedModulesAsync(
                    preemptedKinds.Contains(candidate.Kind)
                        ? $"P-HPR {candidate.Kind} stopped because a higher-priority slip/lock effect claimed the target module."
                        : $"P-HPR {candidate.Kind} stopped because {candidate.Reason}.",
                    nowUtc,
                    countAsWatchdog: false,
                    cancellationToken,
                    candidate.Kind).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask StopOwnedModulesAsync(
        string reason,
        DateTimeOffset nowUtc,
        bool countAsWatchdog,
        CancellationToken cancellationToken,
        PHprPedalEffectKind? onlyKind = null)
    {
        List<StopPlan> stopPlans;
        lock (_gate)
        {
            stopPlans = CreateStopPlansLocked(onlyKind);
            if (stopPlans.Count == 0)
            {
                _runtimeState = HasActiveModulesLocked() ? "Active" : "Idle";
                _lastSlipLockStopReason = reason;
                return;
            }

            _runtimeState = "Stopping";
            if (countAsWatchdog)
            {
                _watchdogStopCount++;
            }
        }

        foreach (var stopPlan in stopPlans)
        {
            var command = PHprCommand.Create(
                stopPlan.TargetModule.ToModuleId(),
                0d,
                PHprSafetyLimits.Default.MinFrequencyHz,
                0,
                stopPlan.Kind.ToCommandSource(),
                stopPlan.Priority,
                nowUtc);
            var result = await _output.SendAsync(command, cancellationToken).ConfigureAwait(false);
            StoreStopResult(stopPlan, result, nowUtc, reason, countAsWatchdog);
        }
    }

    private List<StopPlan> CreateStopPlansLocked(PHprPedalEffectKind? onlyKind)
    {
        var plans = new List<StopPlan>(capacity: 2);
        foreach (var kind in new[] { PHprPedalEffectKind.WheelSlip, PHprPedalEffectKind.WheelLock })
        {
            if (onlyKind is not null && onlyKind.Value != kind)
            {
                continue;
            }

            var modules = new List<PHprModuleId>(capacity: 2);
            for (var moduleIndex = 0; moduleIndex < ModuleCount; moduleIndex++)
            {
                if (_activeModuleOwners[moduleIndex] == kind)
                {
                    modules.Add(ToModule(moduleIndex));
                }
            }

            if (modules.Count == 0)
            {
                continue;
            }

            plans.Add(new StopPlan(
                kind,
                ToTarget(modules),
                _options.GetSettings(kind).Priority,
                modules));
        }

        return plans;
    }

    private void StoreStopResult(
        StopPlan stopPlan,
        PHprCommandResult result,
        DateTimeOffset nowUtc,
        string reason,
        bool countAsWatchdog)
    {
        var index = ToIndex(stopPlan.Kind);
        lock (_gate)
        {
            foreach (var module in stopPlan.Modules)
            {
                _activeModuleOwners[ToModuleIndex(module)] = null;
            }

            _lastCommand = result.Command;
            _lastOutputResult = result;
            _lastTargets[index] = stopPlan.TargetModule;
            _lastCommands[index] = result.Command;
            _lastOutputResults[index] = result;
            _lastActive[index] = false;
            _lastReasons[index] = reason;
            _lastIntensity[index] = 0d;
            _lastStrength[index] = 0d;
            _lastFrequency[index] = 0d;
            _lastStopAtUtc[index] = nowUtc;
            _stopCommandCounts[index]++;
            _stopCommandCount++;

            _lastSlipLockStopReason = $"{reason} {result.Message}".Trim();
            _runtimeState = HasActiveModulesLocked() ? "Active" : result.Succeeded ? "Idle" : "StopRejected";
            _lastTargetModule = stopPlan.TargetModule;
        }
    }

    private void StoreCompleted(
        PHprSlipLockRoutingResult result,
        IReadOnlyList<RoutePlan> plans)
    {
        var lastCommand = result.Commands.LastOrDefault();
        var lastPlan = plans.LastOrDefault();
        lock (_gate)
        {
            _lastActiveEffect = plans.OrderByDescending(plan => plan.Priority).FirstOrDefault()?.Kind;
            _lastTargetModule = lastCommand?.TargetModule ?? lastPlan?.TargetModule;
            _lastCommand = lastCommand?.Command;
            _lastOutputResult = lastCommand?.OutputResult;
            _lastResult = result;
            _lastIgnoredReason = null;
            _lastError = null;
            _runtimeState = HasActiveModulesLocked() ? "Active" : "Idle";
        }
    }

    private PHprSlipLockRoutingResult StoreIgnored(
        PHprSlipLockRoutingStatus status,
        string message,
        DateTimeOffset nowUtc,
        double intensity01 = 0d)
    {
        var result = new PHprSlipLockRoutingResult(
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
            _lastIgnoredReason = message;
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
            _lastIgnoredReason = result.Message;
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
            _intervalSuppressedCount++;
        }
    }

    private void IncrementStaleTelemetrySuppressed(PHprPedalEffectKind kind)
    {
        lock (_gate)
        {
            _staleTelemetrySuppressedCounts[ToIndex(kind)]++;
            _staleTelemetrySuppressedCount++;
        }
    }

    private void IncrementGearProtectionSuppressed()
    {
        lock (_gate)
        {
            _gearProtectionSuppressedCount++;
        }
    }

    private void MarkEffectActiveLocked(
        PHprPedalEffectKind kind,
        IReadOnlyList<PHprModuleId> modules,
        DateTimeOffset nowUtc)
    {
        var index = ToIndex(kind);
        var wasActive = modules.Any(module => _activeModuleOwners[ToModuleIndex(module)] == kind);
        foreach (var module in modules)
        {
            _activeModuleOwners[ToModuleIndex(module)] = kind;
        }

        _lastStartAtUtc[index] = wasActive ? _lastStartAtUtc[index] : nowUtc;
        _lastUpdateAtUtc[index] = nowUtc;
        _lastSlipLockStopReason = "none";
        _runtimeState = "Active";
    }

    private bool IsGearProtectionActive(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            return _lastGearPulseAtUtc is not null
                && nowUtc - _lastGearPulseAtUtc.Value < TimeSpan.FromMilliseconds(GearProtectionWindowMs);
        }
    }

    private bool HasActiveModulesLocked()
    {
        return _activeModuleOwners.Any(owner => owner is not null);
    }

    private string FormatActiveModulesLocked()
    {
        var brakeOwner = _activeModuleOwners[ToModuleIndex(PHprModuleId.Brake)] is not null;
        var throttleOwner = _activeModuleOwners[ToModuleIndex(PHprModuleId.Throttle)] is not null;
        return (brakeOwner, throttleOwner) switch
        {
            (true, true) => PHprModuleId.Both.ToString(),
            (true, false) => PHprModuleId.Brake.ToString(),
            (false, true) => PHprModuleId.Throttle.ToString(),
            _ => "none"
        };
    }

    private static PHprSlipLockTelemetrySnapshot BuildTelemetrySnapshot(
        SlipLockEvaluationResult evaluation)
    {
        return new PHprSlipLockTelemetrySnapshot(
            SpeedKph: evaluation.SpeedKph,
            Throttle01: evaluation.Throttle01,
            Brake01: evaluation.Brake01,
            MaximumSlipRatio: evaluation.MaximumSlipRatio,
            MaximumSlipAngle: evaluation.MaximumSlipAngleRadians,
            MinimumWheelSpeedMetersPerSecond: evaluation.HasMinimumWheelSpeed
                ? evaluation.MinimumWheelSpeedMetersPerSecond
                : 0f,
            MinimumWheelSpeedRatio: evaluation.HasMinimumWheelSpeedRatio
                ? evaluation.MinimumWheelSpeedRatio
                : 0f,
            TelemetryFresh: evaluation.TelemetryFresh,
            MotionExFresh: evaluation.MotionExFresh,
            TractionControlActive: evaluation.TractionControlActive,
            AntiLockBrakesActive: evaluation.AntiLockBrakesActive);
    }

    private static bool IsCommandRateSuppression(PHprCommandResult result)
    {
        return result.Status == PHprCommandStatus.RejectedSafetyLimit
            && result.Message.Contains("command rate", StringComparison.OrdinalIgnoreCase);
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

    private static PHprModuleId ToModule(int moduleIndex)
    {
        return moduleIndex == ToModuleIndex(PHprModuleId.Throttle)
            ? PHprModuleId.Throttle
            : PHprModuleId.Brake;
    }

    private static int ToIndex(PHprPedalEffectKind kind)
    {
        return kind == PHprPedalEffectKind.WheelLock ? 1 : 0;
    }

    private static int ToModuleIndex(PHprModuleId module)
    {
        return module == PHprModuleId.Throttle ? 1 : 0;
    }

    private static DateTimeOffset? MaxTimestamp(IReadOnlyList<DateTimeOffset?> timestamps)
    {
        var values = timestamps
            .Where(timestamp => timestamp is not null)
            .Select(timestamp => timestamp!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private sealed record EffectCandidate(
        PHprPedalEffectKind Kind,
        PHprSlipLockEffectSettings Settings,
        bool IsActive,
        bool IsTelemetryStale,
        double Intensity01,
        double ComputedStrength01,
        double ComputedFrequencyHz,
        int DurationMs,
        bool BelowTactileThreshold,
        string Reason,
        PHprSlipLockTelemetrySnapshot Telemetry)
    {
        public static EffectCandidate Active(
            PHprPedalEffectKind kind,
            PHprSlipLockEffectSettings settings,
            double intensity01,
            string reason,
            PHprSlipLockTelemetrySnapshot telemetry)
        {
            var computedStrength = settings.ScaleStrength(intensity01);
            var computedFrequency = settings.ScaleFrequency(intensity01);
            return new EffectCandidate(
                kind,
                settings,
                true,
                false,
                intensity01,
                computedStrength,
                computedFrequency,
                settings.DurationMs,
                computedStrength < TactileThresholdStrength01,
                reason,
                telemetry);
        }

        public static EffectCandidate Inactive(
            PHprPedalEffectKind kind,
            PHprSlipLockEffectSettings settings,
            string reason,
            PHprSlipLockTelemetrySnapshot telemetry)
        {
            return new EffectCandidate(
                kind,
                settings,
                false,
                false,
                0d,
                0d,
                0d,
                settings.DurationMs,
                false,
                reason,
                telemetry);
        }

        public static EffectCandidate Stale(
            PHprPedalEffectKind kind,
            PHprSlipLockEffectSettings settings,
            string reason,
            PHprSlipLockTelemetrySnapshot telemetry)
        {
            return new EffectCandidate(
                kind,
                settings,
                false,
                true,
                0d,
                0d,
                0d,
                settings.DurationMs,
                false,
                reason,
                telemetry);
        }
    }

    private sealed record RoutePlan(
        PHprPedalEffectKind Kind,
        PHprGearPulseTarget TargetModule,
        double Strength01,
        double FrequencyHz,
        int DurationMs,
        int Priority,
        IReadOnlyList<PHprModuleId> Modules,
        double Intensity01);

    private sealed record StopPlan(
        PHprPedalEffectKind Kind,
        PHprGearPulseTarget TargetModule,
        int Priority,
        IReadOnlyList<PHprModuleId> Modules);

    private sealed record RoutePlanningResult(
        IReadOnlyList<RoutePlan> Plans,
        IReadOnlySet<PHprPedalEffectKind> PreemptedKinds,
        double MaximumIntensity01);
}
