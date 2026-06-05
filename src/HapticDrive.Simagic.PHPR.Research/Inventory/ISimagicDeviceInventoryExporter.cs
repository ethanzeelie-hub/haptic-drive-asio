namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public interface ISimagicDeviceInventoryExporter
{
    ValueTask<string> ExportJsonAsync(
        SimagicDeviceInventorySnapshot snapshot,
        string directoryPath,
        CancellationToken cancellationToken = default);

    ValueTask<string> ExportMarkdownSummaryAsync(
        SimagicDeviceInventorySnapshot snapshot,
        string directoryPath,
        CancellationToken cancellationToken = default);
}
