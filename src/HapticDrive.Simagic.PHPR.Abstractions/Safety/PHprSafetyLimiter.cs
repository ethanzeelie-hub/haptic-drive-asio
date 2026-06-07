using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public sealed class PHprSafetyLimiter : IPHprSafetyLimiter
{
    private static readonly TimeSpan CommandRateWindow = TimeSpan.FromSeconds(1);
    private readonly object _gate = new();
    private readonly IPHprSafetyClock _clock;
    private readonly Queue<DateTimeOffset> _acceptedStartCommandTimes = new();
    private DateTimeOffset? _brakeContinuousUntilUtc;
    private DateTimeOffset? _throttleContinuousUntilUtc;
    private long _totalEvaluatedCommandCount;
    private long _acceptedCount;
    private long _acceptedWithClampCount;
    private long _rejectedCount;
    private long _emergencyStopCount;
    private PHprSafetyDecision? _lastDecision;
    private PHprSafetyViolation? _lastViolation;
    private PHprCommand? _lastAcceptedCommand;
    private PHprCommand? _lastRejectedCommand;
    private PHprSafetyClampDetails? _lastClampDetails;
    private string? _lastError;
    private bool _emergencyStopActive;

    public PHprSafetyLimiter(PHprSafetyLimits? limits = null, IPHprSafetyClock? clock = null)
    {
        Limits = limits ?? PHprSafetyLimits.Default;
        _clock = clock ?? SystemPHprSafetyClock.Instance;
    }

    public PHprSafetyLimits Limits { get; }

    public PHprSafetyDecision Evaluate(PHprCommand command, PHprSafetyContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_gate)
        {
            var now = _clock.UtcNow;
            var effectiveContext = context ?? PHprSafetyContext.DefaultMock;
            PruneCommandRateWindow(now);
            _totalEvaluatedCommandCount++;

            try
            {
                var decision = EvaluateLocked(command, effectiveContext, now);
                RecordDecisionLocked(decision);
                return decision;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                var decision = PHprSafetyDecision.RejectedCommand(
                    command,
                    new PHprSafetyViolation(PHprSafetyViolationCode.Unknown, $"P-HPR safety evaluation failed: {ex.Message}"),
                    now) with
                { Kind = PHprSafetyDecisionKind.Failed };
                RecordDecisionLocked(decision);
                return decision;
            }
        }
    }

    public PHprSafetyDecision RecordEmergencyStop(PHprSafetyContext? context = null)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            PruneCommandRateWindow(now);
            _emergencyStopActive = true;
            _brakeContinuousUntilUtc = null;
            _throttleContinuousUntilUtc = null;
            _acceptedStartCommandTimes.Clear();
            _emergencyStopCount++;

            var decision = PHprSafetyDecision.EmergencyStopped(now);
            RecordDecisionLocked(decision, countAsEvaluation: false);
            return decision;
        }
    }

    public void ClearEmergencyStop()
    {
        lock (_gate)
        {
            _emergencyStopActive = false;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _acceptedStartCommandTimes.Clear();
            _brakeContinuousUntilUtc = null;
            _throttleContinuousUntilUtc = null;
            _totalEvaluatedCommandCount = 0;
            _acceptedCount = 0;
            _acceptedWithClampCount = 0;
            _rejectedCount = 0;
            _emergencyStopCount = 0;
            _lastDecision = null;
            _lastViolation = null;
            _lastAcceptedCommand = null;
            _lastRejectedCommand = null;
            _lastClampDetails = null;
            _lastError = null;
            _emergencyStopActive = false;
        }
    }

    public PHprSafetySnapshot GetSnapshot(PHprSafetyContext? context = null)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            PruneCommandRateWindow(now);
            var effectiveContext = context ?? PHprSafetyContext.DefaultMock;
            return new PHprSafetySnapshot(
                Limits,
                effectiveContext,
                _totalEvaluatedCommandCount,
                _acceptedCount,
                _acceptedWithClampCount,
                _rejectedCount,
                _emergencyStopCount,
                _lastDecision,
                _lastViolation,
                _lastAcceptedCommand,
                _lastRejectedCommand,
                _lastClampDetails,
                _acceptedStartCommandTimes.Count,
                CalculateContinuousEstimateMs(now),
                _emergencyStopActive || effectiveContext.EmergencyStopActive,
                Limits.AllowRealDeviceWrites,
                !Limits.AllowRealDeviceWrites,
                _lastError,
                _lastDecision?.EvaluatedAtUtc);
        }
    }

    private PHprSafetyDecision EvaluateLocked(
        PHprCommand command,
        PHprSafetyContext context,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(command.TargetModule) || !Enum.IsDefined(command.Source))
        {
            return Reject(command, PHprSafetyViolationCode.InvalidCommand, "P-HPR command has an invalid target module or source.", now);
        }

        var isEmergencyStop = IsEmergencyStop(command);
        var isStop = IsSafeStop(command);
        if (isEmergencyStop)
        {
            _emergencyStopActive = true;
            _brakeContinuousUntilUtc = null;
            _throttleContinuousUntilUtc = null;
            _acceptedStartCommandTimes.Clear();
            _emergencyStopCount++;
            return PHprSafetyDecision.EmergencyStopped(now);
        }

        if (isStop)
        {
            ClearContinuousForTarget(command.TargetModule);
            return PHprSafetyDecision.AcceptedCommand(
                command,
                command with { SafetyFlags = command.SafetyFlags | PHprSafetyFlags.MockOnly },
                null,
                now);
        }

        var violation = GetBlockingViolation(command, context);
        if (violation is not null)
        {
            return PHprSafetyDecision.RejectedCommand(command, violation, now);
        }

        var clamped = command.ClampTo(Limits);
        var clampDetails = CreateClampDetails(command, clamped);
        var continuousViolation = GetContinuousDurationViolation(clamped, now);
        if (continuousViolation is not null)
        {
            return PHprSafetyDecision.RejectedCommand(command, continuousViolation, now);
        }

        if (_acceptedStartCommandTimes.Count >= Math.Max(1, Limits.MaxCommandsPerSecond))
        {
            return Reject(command, PHprSafetyViolationCode.CommandRateExceeded, "P-HPR command rate exceeded the configured safety limit.", now);
        }

        _acceptedStartCommandTimes.Enqueue(now);
        ExtendContinuousForTarget(clamped.TargetModule, clamped.DurationMs, now);

        return PHprSafetyDecision.AcceptedCommand(
            command,
            clamped with { SafetyFlags = clamped.SafetyFlags | PHprSafetyFlags.MockOnly },
            clampDetails,
            now);
    }

    private PHprSafetyViolation? GetBlockingViolation(PHprCommand command, PHprSafetyContext context)
    {
        if (_emergencyStopActive || context.EmergencyStopActive)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.EmergencyStopActive,
                "P-HPR emergency stop is active; start command rejected until safety state is cleared.");
        }

        if (context.RequiresRealDeviceWrites && !Limits.AllowRealDeviceWrites)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.RealWritesNotAllowed,
                "Real P-HPR writes are not allowed by the active safety limits.");
        }

        if (!context.IsDeviceConnected)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.DeviceDisconnected,
                "P-HPR output device is disconnected; start command rejected.");
        }

        if (!IsTargetAvailable(command.TargetModule, context))
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.ModuleUnavailable,
                "Requested P-HPR target module is unavailable; start command rejected.");
        }

        if (context.TelemetryStale)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.TelemetryStale,
                "Telemetry is stale; P-HPR start command rejected.");
        }

        if (context.HapticsStopped)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.HapticsStopped,
                "Haptics are stopped; P-HPR start command rejected.");
        }

        if (context.EmergencyMuteActive)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.EmergencyMuteActive,
                "Emergency mute is active; P-HPR start command rejected.");
        }

        if (!context.DrivingArmed)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.DrivingNotArmed,
                "DrivingArmed is false; P-HPR start command rejected.");
        }

        if (context.SoftwareConflictStatus == PHprSoftwareConflictStatus.ActiveConflict)
        {
            return new PHprSafetyViolation(
                PHprSafetyViolationCode.SimProConflict,
                "SimPro/SimHub conflict placeholder is active; P-HPR start command rejected.");
        }

        return null;
    }

    private PHprSafetyViolation? GetContinuousDurationViolation(PHprCommand command, DateTimeOffset now)
    {
        var maxContinuousDuration = Math.Max(0, Limits.MaxContinuousDurationMs);
        foreach (var target in ExpandTarget(command.TargetModule))
        {
            var continuousUntil = target == PHprModuleId.Brake
                ? _brakeContinuousUntilUtc
                : _throttleContinuousUntilUtc;
            var existingMs = CalculateRemainingMs(continuousUntil, now);
            var projectedMs = existingMs + Math.Max(0, command.DurationMs);
            if (projectedMs > maxContinuousDuration)
            {
                return new PHprSafetyViolation(
                    PHprSafetyViolationCode.ContinuousDurationExceeded,
                    $"P-HPR continuous-duration estimate {projectedMs} ms exceeds the configured {maxContinuousDuration} ms limit.");
            }
        }

        return null;
    }

    private void RecordDecisionLocked(PHprSafetyDecision decision, bool countAsEvaluation = true)
    {
        _lastDecision = decision;
        _lastViolation = decision.Violation.Code == PHprSafetyViolationCode.None
            ? null
            : decision.Violation;
        _lastClampDetails = decision.ClampDetails?.HasClamp == true
            ? decision.ClampDetails
            : null;

        if (!countAsEvaluation)
        {
            return;
        }

        switch (decision.Kind)
        {
            case PHprSafetyDecisionKind.Accepted:
                _acceptedCount++;
                _lastAcceptedCommand = decision.Command;
                break;
            case PHprSafetyDecisionKind.AcceptedWithClamp:
                _acceptedCount++;
                _acceptedWithClampCount++;
                _lastAcceptedCommand = decision.Command;
                break;
            case PHprSafetyDecisionKind.Rejected:
            case PHprSafetyDecisionKind.Failed:
                _rejectedCount++;
                _lastRejectedCommand = decision.OriginalCommand;
                break;
            case PHprSafetyDecisionKind.EmergencyStopped:
            case PHprSafetyDecisionKind.IgnoredSafeStop:
                break;
        }
    }

    private void PruneCommandRateWindow(DateTimeOffset now)
    {
        while (_acceptedStartCommandTimes.TryPeek(out var timestamp)
            && now - timestamp >= CommandRateWindow)
        {
            _acceptedStartCommandTimes.Dequeue();
        }
    }

    private void ExtendContinuousForTarget(PHprModuleId targetModule, int durationMs, DateTimeOffset now)
    {
        foreach (var target in ExpandTarget(targetModule))
        {
            var continuousUntil = target == PHprModuleId.Brake
                ? _brakeContinuousUntilUtc
                : _throttleContinuousUntilUtc;
            var start = continuousUntil.HasValue && continuousUntil.Value > now
                ? continuousUntil.Value
                : now;
            var until = start.AddMilliseconds(Math.Max(0, durationMs));
            if (target == PHprModuleId.Brake)
            {
                _brakeContinuousUntilUtc = until;
            }
            else
            {
                _throttleContinuousUntilUtc = until;
            }
        }
    }

    private void ClearContinuousForTarget(PHprModuleId targetModule)
    {
        foreach (var target in ExpandTarget(targetModule))
        {
            if (target == PHprModuleId.Brake)
            {
                _brakeContinuousUntilUtc = null;
            }
            else
            {
                _throttleContinuousUntilUtc = null;
            }
        }
    }

    private int CalculateContinuousEstimateMs(DateTimeOffset now)
    {
        return Math.Max(
            CalculateRemainingMs(_brakeContinuousUntilUtc, now),
            CalculateRemainingMs(_throttleContinuousUntilUtc, now));
    }

    private static int CalculateRemainingMs(DateTimeOffset? continuousUntilUtc, DateTimeOffset now)
    {
        if (continuousUntilUtc is null || continuousUntilUtc.Value <= now)
        {
            return 0;
        }

        return (int)Math.Ceiling((continuousUntilUtc.Value - now).TotalMilliseconds);
    }

    private static PHprSafetyClampDetails? CreateClampDetails(PHprCommand original, PHprCommand clamped)
    {
        var details = new PHprSafetyClampDetails(
            StrengthClamped: HasFlag(PHprSafetyFlags.ClampedStrength),
            DurationClamped: HasFlag(PHprSafetyFlags.ClampedDuration),
            FrequencyClamped: HasFlag(PHprSafetyFlags.ClampedFrequency),
            original.Strength01,
            clamped.Strength01,
            original.DurationMs,
            clamped.DurationMs,
            original.FrequencyHz,
            clamped.FrequencyHz);

        return details.HasClamp ? details : null;

        bool HasFlag(PHprSafetyFlags flag)
        {
            return clamped.SafetyFlags.HasFlag(flag) && !original.SafetyFlags.HasFlag(flag);
        }
    }

    private static PHprSafetyDecision Reject(
        PHprCommand command,
        PHprSafetyViolationCode violationCode,
        string message,
        DateTimeOffset now)
    {
        return PHprSafetyDecision.RejectedCommand(
            command,
            new PHprSafetyViolation(violationCode, message),
            now);
    }

    private static bool IsTargetAvailable(PHprModuleId targetModule, PHprSafetyContext context)
    {
        return targetModule switch
        {
            PHprModuleId.Brake => context.BrakeModuleAvailable,
            PHprModuleId.Throttle => context.ThrottleModuleAvailable,
            PHprModuleId.Both => context.BrakeModuleAvailable && context.ThrottleModuleAvailable,
            _ => false
        };
    }

    private static IReadOnlyList<PHprModuleId> ExpandTarget(PHprModuleId targetModule)
    {
        return targetModule switch
        {
            PHprModuleId.Brake => [PHprModuleId.Brake],
            PHprModuleId.Throttle => [PHprModuleId.Throttle],
            PHprModuleId.Both => [PHprModuleId.Brake, PHprModuleId.Throttle],
            _ => []
        };
    }

    private static bool IsEmergencyStop(PHprCommand command)
    {
        return command.Source == PHprCommandSource.EmergencyStop
            || command.SafetyFlags.HasFlag(PHprSafetyFlags.EmergencyStop);
    }

    private static bool IsSafeStop(PHprCommand command)
    {
        return command.DurationMs <= 0 || command.Strength01 <= 0d;
    }
}
