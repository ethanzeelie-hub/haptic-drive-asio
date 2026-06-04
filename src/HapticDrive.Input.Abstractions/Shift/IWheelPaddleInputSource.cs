namespace HapticDrive.Input.Abstractions.Shift;

public interface IWheelPaddleInputSource : IShiftIntentSource
{
    string? SelectedDeviceId { get; }
}
