namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record InputEventTimestamp(DateTimeOffset Utc, long StopwatchTicks);
