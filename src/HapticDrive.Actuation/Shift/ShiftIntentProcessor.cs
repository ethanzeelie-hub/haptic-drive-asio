using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Shift;

public sealed class ShiftIntentProcessor : IShiftIntentSource
{
    private readonly object _gate = new();
    private readonly IDrivingArmedStateProvider _drivingArmedStateProvider;
    private readonly IShiftIntentSink _sink;
    private ShiftIntentProcessorOptions _options;
    private ShiftIntentTelemetrySnapshot _lastTelemetry = ShiftIntentTelemetrySnapshot.None;
    private long _totalPaddleEventsObserved;
    private long _acceptedShiftIntentCount;
    private long _suppressedShiftIntentCount;
    private long _pendingConfirmationCount;
    private PaddleSide _lastPaddleSide = PaddleSide.Unknown;
    private ShiftIntentDirection _lastDirection = ShiftIntentDirection.Unknown;
    private WheelPaddleInputEvent? _lastPaddleEvent;
    private ShiftIntentEvent? _lastAcceptedEvent;
    private ShiftIntentEvaluationResult? _lastSuppressedEvent;
    private string? _lastSuppressionReason;
    private DrivingArmedState _lastDrivingArmedState = DrivingArmedState.Default;
    private string? _lastError;

    public ShiftIntentProcessor(
        IDrivingArmedStateProvider drivingArmedStateProvider,
        ShiftIntentProcessorOptions? options = null,
        IShiftIntentSink? sink = null)
    {
        _drivingArmedStateProvider = drivingArmedStateProvider
            ?? throw new ArgumentNullException(nameof(drivingArmedStateProvider));
        _options = (options ?? ShiftIntentProcessorOptions.Default).Normalize();
        _sink = sink ?? new InMemoryShiftIntentSink();
    }

    public event EventHandler<ShiftIntentEvent>? ShiftIntentReceived;

    public ShiftIntentEvaluationResult HandlePaddleInput(WheelPaddleInputEvent paddleEvent)
    {
        ArgumentNullException.ThrowIfNull(paddleEvent);

        try
        {
            ShiftIntentProcessorOptions options;
            ShiftIntentTelemetrySnapshot telemetry;
            lock (_gate)
            {
                options = _options;
                telemetry = _lastTelemetry;
            }

            var evaluatedAtUtc = DateTimeOffset.UtcNow;
            var direction = ShiftIntentEvent.DirectionForPaddle(paddleEvent.PaddleSide);
            var drivingArmedState = _drivingArmedStateProvider.Current;

            if (paddleEvent.PaddleSide == PaddleSide.Unknown)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options.Mode,
                    direction,
                    drivingArmedState,
                    "Unknown paddle side.",
                    "Shift intent was suppressed because the paddle side is unknown.",
                    evaluatedAtUtc);
            }

            if (!options.IsEnabled)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options.Mode,
                    direction,
                    drivingArmedState,
                    "Shift intent layer disabled.",
                    "Shift intent was suppressed because the Stage 2F layer is disabled.",
                    evaluatedAtUtc);
            }

            if (!drivingArmedState.IsArmed)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options.Mode,
                    direction,
                    drivingArmedState,
                    drivingArmedState.Reason,
                    $"Shift intent was suppressed by DrivingArmed: {drivingArmedState.Reason}",
                    evaluatedAtUtc);
            }

            if (options.Mode == ShiftIntentMode.TelemetryConfirmedOnly)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options.Mode,
                    direction,
                    drivingArmedState,
                    "TelemetryConfirmedOnly mode is active.",
                    "Mapped paddle press was observed diagnostically; immediate ShiftIntentEvent emission is disabled in TelemetryConfirmedOnly mode.",
                    evaluatedAtUtc);
            }

            var shiftIntentEvent = ShiftIntentEvent.CreatePaddlePress(
                paddleEvent.PaddleSide,
                drivingArmedState,
                paddleEvent.TimestampUtc,
                paddleEvent.SequenceNumber,
                paddleEvent.SourceDevice?.DeviceId,
                telemetry.LastKnownGear,
                direction,
                ShiftIntentSource.WheelPaddle,
                options.Mode,
                paddleEvent.StopwatchTicks,
                paddleEvent.ButtonId,
                telemetry.LastKnownSpeedKph,
                telemetry.LastKnownRpm,
                telemetry.LastKnownSessionTime,
                telemetry.LastKnownFrameIdentifier,
                acceptedAtUtc: evaluatedAtUtc);
            var message = options.Mode == ShiftIntentMode.InstantWithRejectedShiftFeedback
                ? "Shift intent accepted immediately; telemetry rejection feedback remains diagnostics-only in Stage 2F."
                : "Shift intent accepted immediately from mapped paddle input.";
            var result = ShiftIntentEvaluationResult.Accepted(
                shiftIntentEvent,
                paddleEvent,
                message,
                evaluatedAtUtc);

            StoreAccepted(result, options.Mode == ShiftIntentMode.InstantWithRejectedShiftFeedback);
            PublishAcceptedEvent(shiftIntentEvent);
            return result;
        }
        catch (Exception ex)
        {
            var evaluatedAtUtc = DateTimeOffset.UtcNow;
            var fallbackState = DrivingArmedState.NotArmed($"Shift intent evaluation error: {ex.Message}", evaluatedAtUtc);
            var result = ShiftIntentEvaluationResult.Suppressed(
                paddleEvent,
                GetMode(),
                ShiftIntentEvent.DirectionForPaddle(paddleEvent.PaddleSide),
                fallbackState,
                ex.Message,
                $"Shift intent evaluation error: {ex.Message}",
                evaluatedAtUtc);
            StoreError(result, ex.Message);
            return result;
        }
    }

    public void Configure(ShiftIntentProcessorOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? ShiftIntentProcessorOptions.Default).Normalize();
        }
    }

    public void UpdateTelemetry(
        HapticFrame? frame,
        VehicleState? vehicleState = null,
        DateTimeOffset? lastVehicleStateUpdateAtUtc = null,
        TimeSpan? telemetryAge = null)
    {
        var telemetry = frame is not null
            ? ShiftIntentTelemetrySnapshot.FromHapticFrame(frame, lastVehicleStateUpdateAtUtc, telemetryAge)
            : vehicleState is not null
                ? ShiftIntentTelemetrySnapshot.FromVehicleState(vehicleState, lastVehicleStateUpdateAtUtc, telemetryAge)
                : ShiftIntentTelemetrySnapshot.None;
        lock (_gate)
        {
            _lastTelemetry = telemetry;
        }
    }

    public ShiftIntentDiagnosticsSnapshot GetDiagnosticsSnapshot()
    {
        lock (_gate)
        {
            return new ShiftIntentDiagnosticsSnapshot(
                _options.IsEnabled,
                _options.Mode,
                _totalPaddleEventsObserved,
                _acceptedShiftIntentCount,
                _suppressedShiftIntentCount,
                _lastPaddleSide,
                _lastDirection,
                _lastPaddleEvent,
                _lastAcceptedEvent,
                _lastSuppressedEvent,
                _lastSuppressionReason,
                _lastDrivingArmedState,
                _lastTelemetry,
                _pendingConfirmationCount,
                _lastError);
        }
    }

    public ShiftIntentSourceSnapshot GetSnapshot()
    {
        var snapshot = GetDiagnosticsSnapshot();
        return new ShiftIntentSourceSnapshot(
            snapshot.IsEnabled,
            snapshot.LastAcceptedEvent?.SourceDeviceId ?? snapshot.LastPaddleEvent?.SourceDevice?.DeviceId,
            snapshot.AcceptedShiftIntentCount,
            snapshot.LastPaddleSide,
            snapshot.LastAcceptedEvent?.TimestampUtc,
            snapshot.LastSuppressionReason ?? snapshot.LastError);
    }

    public void ClearDiagnostics()
    {
        lock (_gate)
        {
            _totalPaddleEventsObserved = 0;
            _acceptedShiftIntentCount = 0;
            _suppressedShiftIntentCount = 0;
            _pendingConfirmationCount = 0;
            _lastPaddleSide = PaddleSide.Unknown;
            _lastDirection = ShiftIntentDirection.Unknown;
            _lastPaddleEvent = null;
            _lastAcceptedEvent = null;
            _lastSuppressedEvent = null;
            _lastSuppressionReason = null;
            _lastError = null;
        }

        if (_sink is InMemoryShiftIntentSink inMemorySink)
        {
            inMemorySink.Clear();
        }
    }

    private ShiftIntentMode GetMode()
    {
        lock (_gate)
        {
            return _options.Mode;
        }
    }

    private ShiftIntentEvaluationResult StoreSuppressed(
        WheelPaddleInputEvent paddleEvent,
        ShiftIntentMode mode,
        ShiftIntentDirection direction,
        DrivingArmedState drivingArmedState,
        string suppressionReason,
        string message,
        DateTimeOffset evaluatedAtUtc)
    {
        var result = ShiftIntentEvaluationResult.Suppressed(
            paddleEvent,
            mode,
            direction,
            drivingArmedState,
            suppressionReason,
            message,
            evaluatedAtUtc);

        lock (_gate)
        {
            _totalPaddleEventsObserved++;
            _suppressedShiftIntentCount++;
            _lastPaddleSide = paddleEvent.PaddleSide;
            _lastDirection = direction;
            _lastPaddleEvent = paddleEvent;
            _lastSuppressedEvent = result;
            _lastSuppressionReason = result.SuppressionReason;
            _lastDrivingArmedState = drivingArmedState;
            _lastError = null;
        }

        return result;
    }

    private void StoreAccepted(ShiftIntentEvaluationResult result, bool pendingConfirmation)
    {
        lock (_gate)
        {
            _totalPaddleEventsObserved++;
            _acceptedShiftIntentCount++;
            if (pendingConfirmation)
            {
                _pendingConfirmationCount++;
            }

            _lastPaddleSide = result.PaddleEvent.PaddleSide;
            _lastDirection = result.Direction;
            _lastPaddleEvent = result.PaddleEvent;
            _lastAcceptedEvent = result.ShiftIntentEvent;
            _lastSuppressionReason = null;
            _lastDrivingArmedState = result.DrivingArmedStateAtEvaluation;
            _lastError = null;
        }
    }

    private void StoreError(ShiftIntentEvaluationResult result, string errorMessage)
    {
        lock (_gate)
        {
            _totalPaddleEventsObserved++;
            _suppressedShiftIntentCount++;
            _lastPaddleSide = result.PaddleEvent.PaddleSide;
            _lastDirection = result.Direction;
            _lastPaddleEvent = result.PaddleEvent;
            _lastSuppressedEvent = result;
            _lastSuppressionReason = result.SuppressionReason;
            _lastDrivingArmedState = result.DrivingArmedStateAtEvaluation;
            _lastError = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown shift intent evaluation error." : errorMessage.Trim();
        }
    }

    private void PublishAcceptedEvent(ShiftIntentEvent shiftIntentEvent)
    {
        try
        {
            _sink.OnShiftIntentAccepted(shiftIntentEvent);
            ShiftIntentReceived?.Invoke(this, shiftIntentEvent);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _lastError = $"Accepted ShiftIntentEvent publication failed: {ex.Message}";
            }
        }
    }
}
