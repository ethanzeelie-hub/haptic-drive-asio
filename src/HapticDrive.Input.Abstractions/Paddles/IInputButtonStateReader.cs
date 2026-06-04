using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Abstractions.Paddles;

public interface IInputButtonStateReader
{
    InputDiscoveryMethod Method { get; }

    ValueTask StartAsync(InputDeviceSelection selection, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask<InputButtonStateSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
