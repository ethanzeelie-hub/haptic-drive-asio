using HapticDrive.Input.Abstractions.Driving;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.PHpr;

public sealed class PaddleGearBenchTestController
{
    private readonly object _gate = new();
    private PaddleGearBenchTestOptions _options;
    private long _acceptedBenchGearEventCount;
    private long _suppressedBenchGearEventCount;
    private long _leftPaddleAcceptedCount;
    private long _rightPaddleAcceptedCount;
    private ShiftIntentEvent? _lastAcceptedBenchEvent;
    private WheelPaddleInputEvent? _lastPaddleEvent;
    private string? _lastSuppressionReason;
    private PaddleGearBenchTestResult? _lastResult;
    private string? _lastOutputStatus;
    private string? _lastError;

    public PaddleGearBenchTestController(PaddleGearBenchTestOptions? options = null)
    {
        _options = (options ?? PaddleGearBenchTestOptions.Disabled).Normalize();
    }

    public void Configure(PaddleGearBenchTestOptions options)
    {
        lock (_gate)
        {
            _options = (options ?? PaddleGearBenchTestOptions.Disabled).Normalize();
        }
    }

    public PaddleGearBenchTestResult HandlePaddleInput(
        WheelPaddleInputEvent paddleEvent,
        WheelPaddleMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(paddleEvent);

        PaddleGearBenchTestOptions options;
        lock (_gate)
        {
            options = _options;
        }

        var evaluatedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            if (!options.IsEnabled)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options,
                    "Paddle Gear Bench Test Mode is disabled.",
                    evaluatedAtUtc);
            }

            if (!options.IsArmed)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options,
                    "Paddle Gear Bench Test Mode is not armed.",
                    evaluatedAtUtc);
            }

            if (paddleEvent.ButtonState != InputButtonState.Pressed)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options,
                    $"Paddle Gear Bench Test Mode accepts Pressed events only; received {paddleEvent.ButtonState}.",
                    evaluatedAtUtc);
            }

            var normalizedMapping = (mapping ?? WheelPaddleMapping.Default).Normalize();
            var mappedSide = normalizedMapping.ResolvePaddleSide(paddleEvent.ButtonId);
            if (paddleEvent.PaddleSide == PaddleSide.Unknown
                || mappedSide == PaddleSide.Unknown
                || mappedSide != paddleEvent.PaddleSide)
            {
                return StoreSuppressed(
                    paddleEvent,
                    options,
                    "Paddle event does not match the current mapped left/right paddle buttons.",
                    evaluatedAtUtc);
            }

            var drivingArmed = DrivingArmedState.Armed(
                "Paddle Gear Bench Test Mode armed locally; recent F1 telemetry is not required for this validation event.",
                evaluatedAtUtc);
            var direction = ShiftIntentEvent.DirectionForPaddle(paddleEvent.PaddleSide);
            var shiftIntentEvent = ShiftIntentEvent.CreatePaddlePress(
                paddleEvent.PaddleSide,
                drivingArmed,
                paddleEvent.TimestampUtc,
                paddleEvent.SequenceNumber,
                paddleEvent.SourceDevice?.DeviceId,
                lastTelemetryGear: null,
                direction,
                ShiftIntentSource.Test,
                ShiftIntentMode.InstantPaddleOnly,
                paddleEvent.StopwatchTicks,
                paddleEvent.ButtonId,
                acceptedAtUtc: evaluatedAtUtc);
            var result = PaddleGearBenchTestResult.AcceptedEvent(
                paddleEvent,
                options,
                shiftIntentEvent,
                evaluatedAtUtc);

            StoreAccepted(result);
            return result;
        }
        catch (Exception ex)
        {
            return StoreError(
                paddleEvent,
                options,
                $"Paddle Gear Bench Test Mode failed safely: {ex.Message}",
                evaluatedAtUtc,
                ex.Message);
        }
    }

    public PaddleGearBenchTestSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new PaddleGearBenchTestSnapshot(
                _options,
                _acceptedBenchGearEventCount,
                _suppressedBenchGearEventCount,
                _leftPaddleAcceptedCount,
                _rightPaddleAcceptedCount,
                _lastAcceptedBenchEvent,
                _lastPaddleEvent,
                _lastSuppressionReason,
                _lastResult,
                _lastOutputStatus,
                _lastError);
        }
    }

    public void RecordOutputStatus(string status)
    {
        lock (_gate)
        {
            _lastOutputStatus = string.IsNullOrWhiteSpace(status)
                ? null
                : status.Trim();
        }
    }

    public void ClearDiagnostics()
    {
        lock (_gate)
        {
            _acceptedBenchGearEventCount = 0;
            _suppressedBenchGearEventCount = 0;
            _leftPaddleAcceptedCount = 0;
            _rightPaddleAcceptedCount = 0;
            _lastAcceptedBenchEvent = null;
            _lastPaddleEvent = null;
            _lastSuppressionReason = null;
            _lastResult = null;
            _lastOutputStatus = null;
            _lastError = null;
        }
    }

    private PaddleGearBenchTestResult StoreSuppressed(
        WheelPaddleInputEvent paddleEvent,
        PaddleGearBenchTestOptions options,
        string suppressionReason,
        DateTimeOffset evaluatedAtUtc)
    {
        var result = PaddleGearBenchTestResult.Suppressed(
            paddleEvent,
            options,
            suppressionReason,
            evaluatedAtUtc);

        lock (_gate)
        {
            _suppressedBenchGearEventCount++;
            _lastPaddleEvent = paddleEvent;
            _lastSuppressionReason = suppressionReason;
            _lastResult = result;
            _lastError = null;
        }

        return result;
    }

    private void StoreAccepted(PaddleGearBenchTestResult result)
    {
        lock (_gate)
        {
            _acceptedBenchGearEventCount++;
            if (result.PaddleEvent.PaddleSide == PaddleSide.Left)
            {
                _leftPaddleAcceptedCount++;
            }
            else if (result.PaddleEvent.PaddleSide == PaddleSide.Right)
            {
                _rightPaddleAcceptedCount++;
            }

            _lastAcceptedBenchEvent = result.ShiftIntentEvent;
            _lastPaddleEvent = result.PaddleEvent;
            _lastSuppressionReason = null;
            _lastResult = result;
            _lastError = null;
        }
    }

    private PaddleGearBenchTestResult StoreError(
        WheelPaddleInputEvent paddleEvent,
        PaddleGearBenchTestOptions options,
        string suppressionReason,
        DateTimeOffset evaluatedAtUtc,
        string errorMessage)
    {
        var result = PaddleGearBenchTestResult.Suppressed(
            paddleEvent,
            options,
            suppressionReason,
            evaluatedAtUtc);

        lock (_gate)
        {
            _suppressedBenchGearEventCount++;
            _lastPaddleEvent = paddleEvent;
            _lastSuppressionReason = suppressionReason;
            _lastResult = result;
            _lastError = string.IsNullOrWhiteSpace(errorMessage)
                ? "Unknown paddle gear bench test error."
                : errorMessage.Trim();
        }

        return result;
    }
}
