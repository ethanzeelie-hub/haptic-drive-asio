using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Input.Abstractions.Paddles;

public sealed class WheelPaddleInputProcessor
{
    private readonly object _gate = new();
    private readonly IInputEventClock _clock;
    private readonly Dictionary<int, InputButtonState> _buttonStates = [];
    private readonly Dictionary<int, DateTimeOffset> _lastAcceptedPressUtcByButton = [];
    private InputDeviceSelection? _selectedDevice;
    private WheelPaddleMapping _mapping = WheelPaddleMapping.Default;
    private InputListenerStatus _status = InputListenerStatus.NotConfigured;
    private int? _lastChangedButtonId;
    private InputButtonState _lastChangedButtonState = InputButtonState.Unknown;
    private WheelPaddleInputEvent? _lastPaddleEvent;
    private long _paddlePressCount;
    private string? _lastErrorMessage;
    private DateTimeOffset _statusChangedAtUtc = DateTimeOffset.UtcNow;

    public WheelPaddleInputProcessor(IInputEventClock? clock = null)
    {
        _clock = clock ?? new SystemInputEventClock();
    }

    public event EventHandler<WheelPaddleRawButtonEvent>? RawButtonChanged;

    public event EventHandler<WheelPaddleInputEvent>? PaddleInputReceived;

    public void RefreshSelection(InputDeviceSelection? selection, InputListenerStatus status)
    {
        lock (_gate)
        {
            _selectedDevice = selection;
            RefreshStatusLocked(status, null);
        }
    }

    public void RefreshMapping(WheelPaddleMapping mapping)
    {
        lock (_gate)
        {
            _mapping = mapping.Normalize();
        }
    }

    public void RefreshStatus(InputListenerStatus status, string? errorMessage = null)
    {
        lock (_gate)
        {
            RefreshStatusLocked(status, errorMessage);
        }
    }

    public WheelPaddleInputEvent? ProcessButtonState(
        int buttonId,
        InputButtonState state,
        InputDeviceSelection? sourceDevice = null,
        InputEventTimestamp? timestamp = null)
    {
        if (buttonId <= 0)
        {
            return null;
        }

        var normalizedState = state == InputButtonState.Pressed
            ? InputButtonState.Pressed
            : InputButtonState.Released;
        var eventTimestamp = timestamp ?? _clock.GetTimestamp();
        WheelPaddleRawButtonEvent? rawEvent = null;
        WheelPaddleInputEvent? paddleEvent = null;

        lock (_gate)
        {
            var previousState = _buttonStates.TryGetValue(buttonId, out var previous)
                ? previous
                : InputButtonState.Released;
            if (previousState == normalizedState)
            {
                return null;
            }

            var eventDevice = sourceDevice ?? _selectedDevice;
            _buttonStates[buttonId] = normalizedState;
            _lastChangedButtonId = buttonId;
            _lastChangedButtonState = normalizedState;
            rawEvent = new WheelPaddleRawButtonEvent(eventDevice, buttonId, normalizedState, eventTimestamp);

            var side = _mapping.ResolvePaddleSide(buttonId);
            if (side != PaddleSide.Unknown
                && normalizedState == InputButtonState.Pressed
                && IsOutsideDebounceWindow(buttonId, eventTimestamp.Utc))
            {
                _lastAcceptedPressUtcByButton[buttonId] = eventTimestamp.Utc;
                _paddlePressCount++;
                paddleEvent = new WheelPaddleInputEvent(
                    side,
                    eventDevice,
                    buttonId,
                    eventTimestamp,
                    _paddlePressCount);
                _lastPaddleEvent = paddleEvent;
            }
        }

        RawButtonChanged?.Invoke(this, rawEvent);
        if (paddleEvent is not null)
        {
            PaddleInputReceived?.Invoke(this, paddleEvent);
        }

        return paddleEvent;
    }

    public WheelPaddleInputSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new WheelPaddleInputSnapshot(
                _status,
                _selectedDevice,
                _mapping,
                GetMappedState(_mapping.LeftPaddleButtonId),
                GetMappedState(_mapping.RightPaddleButtonId),
                _lastChangedButtonId,
                _lastChangedButtonState,
                _lastPaddleEvent,
                _paddlePressCount,
                _lastErrorMessage,
                _statusChangedAtUtc);
        }
    }

    private bool IsOutsideDebounceWindow(int buttonId, DateTimeOffset eventUtc)
    {
        if (!_lastAcceptedPressUtcByButton.TryGetValue(buttonId, out var lastAcceptedUtc))
        {
            return true;
        }

        return eventUtc - lastAcceptedUtc >= _mapping.DebounceDuration;
    }

    private InputButtonState GetMappedState(int? buttonId)
    {
        if (buttonId is null)
        {
            return InputButtonState.Released;
        }

        return _buttonStates.TryGetValue(buttonId.Value, out var state)
            ? state
            : InputButtonState.Released;
    }

    private void RefreshStatusLocked(InputListenerStatus status, string? errorMessage)
    {
        if (_status != status)
        {
            _statusChangedAtUtc = DateTimeOffset.UtcNow;
        }

        _status = status;
        _lastErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
    }
}
