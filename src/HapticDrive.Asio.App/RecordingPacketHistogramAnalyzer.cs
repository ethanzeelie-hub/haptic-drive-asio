using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.App;

internal static class RecordingPacketHistogramAnalyzer
{
    public static async Task<string> AnalyzeAsync(
        string recordingPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            return "Packet histogram unavailable: recording path is missing.";
        }

        var loadResult = await TelemetryRecordingFile.LoadAsync(recordingPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!loadResult.Succeeded || loadResult.Recording is null)
        {
            return $"Packet histogram unavailable: {loadResult.Message}";
        }

        var recording = loadResult.Recording;
        if (!string.Equals(recording.Metadata.SourceGame, "F1 25", StringComparison.OrdinalIgnoreCase))
        {
            return $"Packet histogram unavailable: source game {recording.Metadata.SourceGame} is not supported.";
        }

        if (recording.Packets.Count == 0)
        {
            return "Packet histogram: no packets in recording.";
        }

        var countsById = new Dictionary<byte, long>();
        long ignoredCount = 0;
        long failedCount = 0;

        foreach (var packet in recording.Packets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parseResult = F125PacketHeaderParser.Parse(packet.Payload);
            if (parseResult.Succeeded && parseResult.Definition is not null)
            {
                countsById.TryGetValue(parseResult.Definition.Id, out var currentCount);
                countsById[parseResult.Definition.Id] = currentCount + 1;
            }
            else if (parseResult.WasIgnored)
            {
                ignoredCount++;
            }
            else
            {
                failedCount++;
            }
        }

        var entries = countsById
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .Select(item =>
            {
                F125PacketDefinitions.TryGetById(item.Key, out var definition);
                var name = definition?.Name ?? $"Packet {item.Key}";
                return $"{name}#{item.Key}: {item.Value:N0}";
            })
            .ToList();

        if (ignoredCount > 0)
        {
            entries.Add($"Ignored unknown packet IDs: {ignoredCount:N0}");
        }

        if (failedCount > 0)
        {
            entries.Add($"Invalid packet headers: {failedCount:N0}");
        }

        return $"Packet histogram: {string.Join("; ", entries)}.";
    }
}
