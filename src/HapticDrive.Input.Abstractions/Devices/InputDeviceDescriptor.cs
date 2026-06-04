namespace HapticDrive.Input.Abstractions.Devices;

public sealed record InputDeviceDescriptor(
    string DeviceId,
    string DisplayName,
    string Transport,
    ushort? VendorId = null,
    ushort? ProductId = null,
    ushort? UsagePage = null,
    ushort? Usage = null,
    bool IsReadOnly = true);
