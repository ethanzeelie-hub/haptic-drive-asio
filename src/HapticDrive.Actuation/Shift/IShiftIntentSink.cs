using HapticDrive.Input.Abstractions.Shift;

namespace HapticDrive.Actuation.Shift;

public interface IShiftIntentSink
{
    void OnShiftIntentAccepted(ShiftIntentEvent shiftIntentEvent);
}
