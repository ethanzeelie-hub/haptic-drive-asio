namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public interface IPHprSafetyClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemPHprSafetyClock : IPHprSafetyClock
{
    public static SystemPHprSafetyClock Instance { get; } = new();

    private SystemPHprSafetyClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
