using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Abstractions.Paddles;

public sealed record InputDeviceSelection(
    string DeviceId,
    string DisplayName,
    InputDiscoveryMethod Method,
    int? NativeDeviceIndex = null,
    int? ButtonCount = null)
{
    public static InputDeviceSelection FromDeviceInfo(InputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new InputDeviceSelection(
            device.DeviceId,
            device.DisplayName,
            device.DiscoveryMethod,
            device.NativeDeviceIndex,
            device.ButtonCount);
    }
}
