namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public enum SimagicDeviceInventoryMethod
{
    Unknown = 0,
    ExistingInputDiscovery,
    RawInputMetadata,
    WindowsGameController,
    WindowsRegistryHid,
    WindowsRegistryUsb,
    Synthetic
}
