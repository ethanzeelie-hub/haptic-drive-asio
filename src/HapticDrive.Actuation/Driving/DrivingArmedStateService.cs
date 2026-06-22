using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Input.Abstractions.Driving;

namespace HapticDrive.Actuation.Driving;

public sealed class DrivingArmedStateService : IDrivingArmedStateProvider
{
    private readonly object _gate = new();
    private readonly DrivingArmedStateServiceOptions _options;
    private readonly TimeProvider _timeProvider;
    private DrivingArmedState _current = DrivingArmedState.Default;
    private DrivingArmedSuppressionReason _lastSuppressionReason = DrivingArmedSuppressionReason.NoTelemetry;
    private DateTimeOffset _lastEvaluatedAtUtc;
    private TimeSpan? _lastTelemetryAge;

    public DrivingArmedStateService(
        DrivingArmedStateServiceOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _options = (options ?? DrivingArmedStateServiceOptions.Default).Normalize();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastEvaluatedAtUtc = _timeProvider.GetUtcNow();
    }

    public event EventHandler<DrivingArmedState>? DrivingArmedChanged;

    public DrivingArmedState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public DrivingArmedState UpdateFromVehicleState(
        VehicleState vehicleState,
        DrivingArmedEvaluationContext context,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);
        ArgumentNullException.ThrowIfNull(context);
        return UpdateFromHapticFrame(
            LegacyActuationHapticFrameFactory.FromVehicleState(vehicleState),
            vehicleState,
            context,
            nowUtc);
    }

    public DrivingArmedState UpdateFromHapticFrame(
        HapticFrame frame,
        VehicleState vehicleState,
        DrivingArmedEvaluationContext context,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(vehicleState);
        ArgumentNullException.ThrowIfNull(context);

        var now = nowUtc ?? _timeProvider.GetUtcNow();
        var telemetryAge = context.TelemetryAge ?? CalculateAge(context.LastVehicleStateUpdateAtUtc, now);
        var result = Evaluate(frame, vehicleState, context, telemetryAge, now);
        SetCurrent(result.State, result.SuppressionReason, telemetryAge, now);
        return result.State;
    }

    public DrivingArmedStateServiceSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new DrivingArmedStateServiceSnapshot(
                _current,
                _lastSuppressionReason,
                _lastEvaluatedAtUtc,
                _lastTelemetryAge,
                _options.MenuSafeModeEnabled,
                _options.RequireRecentTelemetry,
                _options.TelemetryFreshnessThreshold,
                _options.AllowZeroSpeedActiveDriving,
                _options.DiagnosticsOnlyUnsafeOverride);
        }
    }

    private DrivingArmedEvaluationResult Evaluate(
        HapticFrame frame,
        VehicleState vehicleState,
        DrivingArmedEvaluationContext context,
        TimeSpan? telemetryAge,
        DateTimeOffset nowUtc)
    {
        var telemetryFresh = TryIsFresh(frame, HapticFrameSignalNames.Telemetry);

        if (context.EmergencyMute)
        {
            return NotArmed("Emergency mute is active.", DrivingArmedSuppressionReason.EmergencyMute, telemetryAge, nowUtc);
        }

        if (!context.HapticsRunning)
        {
            return NotArmed("Haptics are stopped.", DrivingArmedSuppressionReason.HapticsStopped, telemetryAge, nowUtc);
        }

        if (_options.DiagnosticsOnlyUnsafeOverride)
        {
            return Armed("Diagnostics-only override is enabled; unsafe for menus.", telemetryAge, nowUtc);
        }

        if (!context.HasRecentTelemetry)
        {
            return NotArmed("No recent valid telemetry has been observed.", DrivingArmedSuppressionReason.NoTelemetry, telemetryAge, nowUtc);
        }

        if (_options.RequireRecentTelemetry)
        {
            if (telemetryAge is null)
            {
                return NotArmed("No telemetry age is available for the cached driving gate.", DrivingArmedSuppressionReason.NoTelemetry, telemetryAge, nowUtc);
            }

            if (context.TelemetryTimedOutMuted || !telemetryFresh || telemetryAge.Value > _options.TelemetryFreshnessThreshold)
            {
                return NotArmed("Telemetry is stale.", DrivingArmedSuppressionReason.StaleTelemetry, telemetryAge, nowUtc);
            }
        }

        if (!HasFreshRequiredDrivingContext(frame))
        {
            return NotArmed(
                "Required session, lap, participant, or player identity context is missing or stale.",
                DrivingArmedSuppressionReason.NoTelemetry,
                telemetryAge,
                nowUtc);
        }

        if (frame.Context.IsPaused)
        {
            var suppressionReason = vehicleState.CarStatus?.Value.NetworkPaused is > 0
                ? DrivingArmedSuppressionReason.NetworkPaused
                : DrivingArmedSuppressionReason.Paused;
            var message = suppressionReason == DrivingArmedSuppressionReason.NetworkPaused
                ? "Game is network paused."
                : "Game is paused.";
            return NotArmed(message, suppressionReason, telemetryAge, nowUtc);
        }

        if (!frame.Context.IsPlayerControlled
            || !frame.Context.AllowsDrivingOutput
            || frame.Context.DrivingPhase is DrivingPhase.Garage or DrivingPhase.Spectating or DrivingPhase.Unknown)
        {
            return NotArmed(
                "Player appears to be in garage, menu, spectating, or an inactive driving state.",
                DrivingArmedSuppressionReason.GarageMenuOrResultState,
                telemetryAge,
                nowUtc);
        }

        var throttle = frame.Signals.Throttle;
        var brake = frame.Signals.Brake;
        var steer = frame.Signals.Steer;
        if (throttle is null || brake is null || steer is null
            || !float.IsFinite(throttle.Value)
            || !float.IsFinite(brake.Value)
            || !float.IsFinite(steer.Value))
        {
            return NotArmed("Vehicle telemetry contains invalid input values.", DrivingArmedSuppressionReason.InvalidVehicleState, telemetryAge, nowUtc);
        }

        var speedKph = (frame.Signals.SpeedMetersPerSecond ?? 0f) * 3.6f;
        var rpm = frame.Signals.EngineRpm ?? vehicleState.Telemetry?.Value.EngineRpm ?? 0;
        if (!_options.AllowZeroSpeedActiveDriving && speedKph == 0f && rpm == 0)
        {
            return NotArmed("Vehicle is not moving and not active.", DrivingArmedSuppressionReason.NotMovingAndNotActive, telemetryAge, nowUtc);
        }

        var reason = _options.MenuSafeModeEnabled
            ? "Active driving telemetry is fresh."
            : "Active driving telemetry is fresh.";
        return Armed(reason, telemetryAge, nowUtc);
    }

    private static bool HasFreshRequiredDrivingContext(HapticFrame frame)
    {
        return frame.Identity.SessionUid is not null
            && frame.Identity.PlayerCarIndex is not null
            && TryIsFresh(frame, HapticFrameSignalNames.Session)
            && TryIsFresh(frame, HapticFrameSignalNames.Lap)
            && TryIsFresh(frame, HapticFrameSignalNames.Participant)
            && TryIsFresh(frame, HapticFrameSignalNames.CarStatus);
    }

    private void SetCurrent(
        DrivingArmedState state,
        DrivingArmedSuppressionReason suppressionReason,
        TimeSpan? telemetryAge,
        DateTimeOffset evaluatedAtUtc)
    {
        DrivingArmedState? changedState = null;
        lock (_gate)
        {
            if (_current != state)
            {
                changedState = state;
            }

            _current = state;
            _lastSuppressionReason = suppressionReason;
            _lastTelemetryAge = telemetryAge;
            _lastEvaluatedAtUtc = evaluatedAtUtc;
        }

        if (changedState is not null)
        {
            DrivingArmedChanged?.Invoke(this, changedState);
        }
    }

    private static DrivingArmedEvaluationResult Armed(
        string reason,
        TimeSpan? telemetryAge,
        DateTimeOffset nowUtc)
    {
        return new DrivingArmedEvaluationResult(
            DrivingArmedState.Armed(reason, nowUtc, telemetryAge),
            DrivingArmedSuppressionReason.None);
    }

    private static DrivingArmedEvaluationResult NotArmed(
        string reason,
        DrivingArmedSuppressionReason suppressionReason,
        TimeSpan? telemetryAge,
        DateTimeOffset nowUtc)
    {
        return new DrivingArmedEvaluationResult(
            DrivingArmedState.NotArmed(reason, nowUtc, telemetryAge),
            suppressionReason);
    }

    private static TimeSpan? CalculateAge(DateTimeOffset? lastUpdateAtUtc, DateTimeOffset nowUtc)
    {
        if (lastUpdateAtUtc is null)
        {
            return null;
        }

        var age = nowUtc - lastUpdateAtUtc.Value;
        return age < TimeSpan.Zero ? TimeSpan.Zero : age;
    }

    private static bool TryIsFresh(HapticFrame frame, string key)
    {
        return frame.Freshness.TryGetValue(key, out var freshness) && freshness.IsFresh;
    }

    private sealed record DrivingArmedEvaluationResult(
        DrivingArmedState State,
        DrivingArmedSuppressionReason SuppressionReason);
}
