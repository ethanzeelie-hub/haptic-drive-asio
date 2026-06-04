namespace HapticDrive.Input.Abstractions.Shift;

public interface IShiftIntentSource
{
    event EventHandler<ShiftIntentEvent>? ShiftIntentReceived;

    ShiftIntentSourceSnapshot GetSnapshot();
}
