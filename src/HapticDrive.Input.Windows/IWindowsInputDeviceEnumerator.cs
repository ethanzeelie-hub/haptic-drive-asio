using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Windows;

public interface IWindowsInputDeviceEnumerator
{
    InputDiscoveryMethod Method { get; }

    IReadOnlyList<InputDeviceInfo> DiscoverDevices(DateTimeOffset discoveredAtUtc);
}
