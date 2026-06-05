using System.Text.Json;
using System.Text.Json.Serialization;

namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed class SimagicDeviceInventoryExporter : ISimagicDeviceInventoryExporter
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async ValueTask<string> ExportJsonAsync(
        SimagicDeviceInventorySnapshot snapshot,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, "simagic-device-inventory-sanitized.json");
        var export = SimagicDeviceInventoryExport.FromSnapshot(snapshot);
        var json = JsonSerializer.Serialize(export, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }

    public async ValueTask<string> ExportMarkdownSummaryAsync(
        SimagicDeviceInventorySnapshot snapshot,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, "simagic-device-inventory-summary.md");
        var summary = SimagicDeviceInventorySummaryFormatter.FormatMarkdown(snapshot);
        await File.WriteAllTextAsync(path, summary, cancellationToken);
        return path;
    }
}
