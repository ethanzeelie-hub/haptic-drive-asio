using HapticDrive.Input.Abstractions.Paddles;

namespace HapticDrive.Input.Abstractions.Shift;

public interface IWheelPaddleInputSource : IShiftIntentSource, IAsyncDisposable
{
    string? SelectedDeviceId { get; }

    event EventHandler<WheelPaddleRawButtonEvent>? RawButtonChanged;

    event EventHandler<WheelPaddleInputEvent>? PaddleInputReceived;

    ValueTask StartAsync(
        InputDeviceSelection selection,
        WheelPaddleMapping mapping,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    void RefreshMapping(WheelPaddleMapping mapping);

    WheelPaddleInputSnapshot GetPaddleSnapshot();
}
