namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed record SimagicDeviceInventorySourceResult(
    IReadOnlyList<SimagicDeviceInventoryItem> Items,
    IReadOnlyList<SimagicDeviceInventoryError> Errors)
{
    public static SimagicDeviceInventorySourceResult Empty { get; } = new([], []);
}
