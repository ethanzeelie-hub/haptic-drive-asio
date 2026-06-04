using System.Diagnostics;

namespace HapticDrive.Input.Abstractions.Paddles;

public interface IInputEventClock
{
    InputEventTimestamp GetTimestamp();
}

public sealed class SystemInputEventClock : IInputEventClock
{
    public InputEventTimestamp GetTimestamp()
    {
        return new InputEventTimestamp(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp());
    }
}
