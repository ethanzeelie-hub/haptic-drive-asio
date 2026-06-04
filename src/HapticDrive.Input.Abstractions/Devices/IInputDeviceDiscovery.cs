namespace HapticDrive.Input.Abstractions.Devices;

public interface IInputDeviceDiscovery
{
    ValueTask<IReadOnlyList<InputDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);
}
