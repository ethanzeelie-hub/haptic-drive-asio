namespace HapticDrive.Input.Abstractions.Devices;

public enum InputDiscoveryMethod
{
    Unknown = 0,
    RawInput,
    WindowsGameController,
    HidMetadata,
    DirectInput,
    SimagicSpecific
}
