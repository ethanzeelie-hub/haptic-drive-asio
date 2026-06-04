using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Input.Abstractions.Driving;

namespace HapticDrive.Actuation.Driving;

public sealed class DrivingArmedStateService : IDrivingArmedStateProvider
{
    private readonly object _gate = new();
    private readonly DrivingArmedStateServiceOptions _options;
    private DrivingArmedState _current = DrivingArmedState.Default;
    private DrivingArmedSuppressionReason _lastSuppressionReason = DrivingArmedSuppressionReason.NoTelemetry;
    private DateTimeOffset _lastEvaluatedAtUtc = DateTimeOffset.UtcNow;
    private TimeSpan? _lastTelemetryAge;

    public DrivingArmedStateService(DrivingArmedStateServiceOptions? options = null)
    {
        _options = (options ?? DrivingArmedStateServiceOptions.Default).Normalize();
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

    public DrivingArmedState UpdateFromPipelineSnapshot(
        HapticPipelineSnapshot snapshot,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var telemetryAge = snapshot.TelemetryAge ?? CalculateAge(snapshot.LastVehicleStateUpdateAtUtc, now);
        var context = new DrivingArmedEvaluationContext
        {
            HapticsRunning = snapshot.IsRunning,
            EmergencyMute = snapshot.EmergencyMute,
            HasRecentTelemetry = snapshot.VehicleStateUpdateCount > 0,
            LastVehicleStateUpdateAtUtc = snapshot.LastVehicleStateUpdateAtUtc,
            TelemetryAge = telemetryAge,
            TelemetryTimedOutMuted = snapshot.TelemetryTimedOutMuted
        };

        return UpdateFromVehicleState(snapshot.VehicleState, context, now);
    }

    public DrivingArmedState UpdateFromVehicleState(
        VehicleState vehicleState,
        DrivingArmedEvaluationContext context,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);
        ArgumentNullException.ThrowIfNull(context);

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var telemetryAge = context.TelemetryAge ?? CalculateAge(context.LastVehicleStateUpdateAtUtc, now);
        var result = Evaluate(vehicleState, context, telemetryAge, now);
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
        VehicleState vehicleState,
        DrivingArmedEvaluationContext context,
        TimeSpan? telemetryAge,
        DateTimeOffset nowUtc)
    {
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

            if (context.TelemetryTimedOutMuted || telemetryAge.Value > _options.TelemetryFreshnessThreshold)
            {
                return NotArmed("Telemetry is stale.", DrivingArmedSuppressionReason.StaleTelemetry, telemetryAge, nowUtc);
            }
        }

        if (vehicleState.Telemetry is null)
        {
            return NotArmed("Vehicle telemetry sample is missing.", DrivingArmedSuppressionReason.InvalidVehicleState, telemetryAge, nowUtc);
        }

        if (_options.MenuSafeModeEnabled)
        {
            var menuSafeResult = EvaluateMenuSafeState(vehicleState, telemetryAge, nowUtc);
            if (menuSafeResult is not null)
            {
                return menuSafeResult;
            }
        }

        var telemetry = vehicleState.Telemetry.Value;
        if (!float.IsFinite(telemetry.Throttle) || !float.IsFinite(telemetry.Brake) || !float.IsFinite(telemetry.Steer))
        {
            return NotArmed("Vehicle telemetry contains invalid input values.", DrivingArmedSuppressionReason.InvalidVehicleState, telemetryAge, nowUtc);
        }

        if (!_options.AllowZeroSpeedActiveDriving
            && telemetry.SpeedKph == 0
            && telemetry.EngineRpm == 0)
        {
            return NotArmed("Vehicle is not moving and not active.", DrivingArmedSuppressionReason.NotMovingAndNotActive, telemetryAge, nowUtc);
        }

        var reason = _options.MenuSafeModeEnabled
            ? "Active driving telemetry is fresh."
            : "Menu safe mode is disabled; armed from recent telemetry only.";
        return Armed(reason, telemetryAge, nowUtc);
    }

    private DrivingArmedEvaluationResult? EvaluateMenuSafeState(
        VehicleState vehicleState,
        TimeSpan? telemetryAge,
        DateTimeOffset nowUtc)
    {
        if (vehicleState.Session is null)
        {
            return NotArmed("Session state is missing for menu-safe gating.", DrivingArmedSuppressionReason.InvalidVehicleState, telemetryAge, nowUtc);
        }

        if (vehicleState.Lap is null)
        {
            return NotArmed("Lap state is missing for menu-safe gating.", DrivingArmedSuppressionReason.InvalidVehicleState, telemetryAge, nowUtc);
        }

        if (vehicleState.Session.Value.GamePaused > 0)
        {
            return NotArmed("Game is paused.", DrivingArmedSuppressionReason.Paused, telemetryAge, nowUtc);
        }

        if (vehicleState.CarStatus?.Value.NetworkPaused is > 0)
        {
            return NotArmed("Network pause is active.", DrivingArmedSuppressionReason.NetworkPaused, telemetryAge, nowUtc);
        }

        if (vehicleState.Lap.Value.DriverStatus == 0)
        {
            return NotArmed("Player appears to be in garage, menu, or an inactive driver state.", DrivingArmedSuppressionReason.GarageMenuOrResultState, telemetryAge, nowUtc);
        }

        if (vehicleState.Lap.Value.ResultStatus is 0 or 1)
        {
            return NotArmed("Player result status is not active driving.", DrivingArmedSuppressionReason.GarageMenuOrResultState, telemetryAge, nowUtc);
        }

        return null;
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

    private sealed record DrivingArmedEvaluationResult(
        DrivingArmedState State,
        DrivingArmedSuppressionReason SuppressionReason);
}
