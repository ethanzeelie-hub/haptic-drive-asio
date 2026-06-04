namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record WheelPaddleInputSourceOptions
{
    public static WheelPaddleInputSourceOptions Default { get; } = new();

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(5);

    public WheelPaddleInputSourceOptions Normalize()
    {
        var interval = PollInterval;
        if (interval <= TimeSpan.Zero)
        {
            interval = TimeSpan.FromMilliseconds(5);
        }

        if (interval > TimeSpan.FromMilliseconds(100))
        {
            interval = TimeSpan.FromMilliseconds(100);
        }

        return this with { PollInterval = interval };
    }
}
