using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record WheelPaddleInputEvent(
    PaddleSide PaddleSide,
    InputDeviceSelection? SourceDevice,
    int ButtonId,
    InputEventTimestamp Timestamp,
    long SequenceNumber,
    InputButtonState ButtonState = InputButtonState.Pressed)
{
    public DateTimeOffset TimestampUtc => Timestamp.Utc;

    public long StopwatchTicks => Timestamp.StopwatchTicks;
}
