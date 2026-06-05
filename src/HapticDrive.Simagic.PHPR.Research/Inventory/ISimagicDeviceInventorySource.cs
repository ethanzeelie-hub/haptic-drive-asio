namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public interface ISimagicDeviceInventorySource
{
    SimagicDeviceInventoryMethod Method { get; }

    ValueTask<SimagicDeviceInventorySourceResult> EnumerateAsync(
        DateTimeOffset discoveredAtUtc,
        CancellationToken cancellationToken = default);
}
