namespace HapticDrive.Input.Abstractions.Devices;

public interface IInputDeviceDiscovery
{
    ValueTask<InputDeviceDiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
}
