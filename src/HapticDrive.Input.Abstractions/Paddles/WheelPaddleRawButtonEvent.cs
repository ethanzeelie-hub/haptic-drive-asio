namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record WheelPaddleRawButtonEvent(
    InputDeviceSelection? SourceDevice,
    int ButtonId,
    InputButtonState State,
    InputEventTimestamp Timestamp)
{
    public DateTimeOffset TimestampUtc => Timestamp.Utc;

    public long StopwatchTicks => Timestamp.StopwatchTicks;
}
