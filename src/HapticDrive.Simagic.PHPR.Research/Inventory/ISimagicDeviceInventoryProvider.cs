namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public interface ISimagicDeviceInventoryProvider
{
    ValueTask<SimagicDeviceInventorySnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
}
