namespace HapticDrive.Simagic.PHPR.Output.Windows;

public interface IPHprDirectStopClock
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemPhprDirectStopClock : IPHprDirectStopClock
{
    public static SystemPhprDirectStopClock Instance { get; } = new();

    private SystemPhprDirectStopClock()
    {
    }

    public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
    }
}
