namespace HapticDrive.Asio.Core.Safety;

public sealed record OutputInterlockSnapshot(
    bool IsLatched,
    OutputInterlockReason Reason,
    string Message,
    DateTimeOffset ChangedAtUtc,
    long Generation)
{
    public bool AllowsOutput => !IsLatched;

    public static OutputInterlockSnapshot StartupSafeDefault()
    {
        return new(
            IsLatched: true,
            Reason: OutputInterlockReason.StartupSafeDefault,
            Message: "Output interlock starts latched until the runtime is explicitly armed.",
            ChangedAtUtc: DateTimeOffset.UtcNow,
            Generation: 0);
    }
}
