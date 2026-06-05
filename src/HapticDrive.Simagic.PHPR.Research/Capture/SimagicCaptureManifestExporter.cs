using System.Text.Json;

namespace HapticDrive.Simagic.PHPR.Research.Capture;

public sealed class SimagicCaptureManifestExporter
{
    public async ValueTask<SimagicCaptureManifest> LoadManifestFromFolderAsync(
        string metadataFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataFolder);

        if (!Directory.Exists(metadataFolder))
        {
            return SimagicCaptureManifest.Create([]);
        }

        var items = new List<SimagicCaptureMetadata>();
        foreach (var path in Directory.EnumerateFiles(metadataFolder, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(path);
            var metadata = await JsonSerializer.DeserializeAsync<SimagicCaptureMetadata>(
                stream,
                SimagicCaptureJson.Options,
                cancellationToken);
            if (metadata is not null)
            {
                items.Add(metadata);
            }
        }

        return SimagicCaptureManifest.Create(items);
    }

    public async ValueTask<string> ExportJsonAsync(
        SimagicCaptureManifest manifest,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "simagic-capture-manifest-sanitized.json");
        var json = JsonSerializer.Serialize(manifest, SimagicCaptureJson.Options);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }
}
