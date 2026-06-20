using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.App;

internal enum RecordingPacketInspectionStatus
{
    Success,
    Unavailable,
    UnsupportedSourceGame
}

internal sealed record RecordingPacketInspectionResult(
    RecordingPacketInspectionStatus Status,
    string Message,
    RecordingPacketInspectionAnalysis? Analysis)
{
    public bool Succeeded => Status == RecordingPacketInspectionStatus.Success && Analysis is not null;

    public static RecordingPacketInspectionResult Success(RecordingPacketInspectionAnalysis analysis)
    {
        return new(RecordingPacketInspectionStatus.Success, "Packet histogram analyzed.", analysis);
    }

    public static RecordingPacketInspectionResult Unavailable(string message)
    {
        return new(RecordingPacketInspectionStatus.Unavailable, message, null);
    }

    public static RecordingPacketInspectionResult UnsupportedSourceGame(string message)
    {
        return new(RecordingPacketInspectionStatus.UnsupportedSourceGame, message, null);
    }
}

internal sealed record RecordingPacketInspectionAnalysis(
    string SourceGame,
    int PacketCount,
    IReadOnlyList<RecordingPacketHistogramEntry> HistogramEntries,
    long IgnoredUnknownPacketCount,
    long InvalidHeaderCount,
    IReadOnlyList<RecordingPacketPreviewEntry> PreviewEntries);

internal sealed record RecordingPacketHistogramEntry(
    string Name,
    byte PacketId,
    long Count);

internal sealed record RecordingPacketPreviewEntry(
    long SequenceNumber,
    TimeSpan RelativeTime,
    string Label,
    int PayloadSizeBytes);

internal static class RecordingPacketHistogramAnalyzer
{
    public static async Task<string> AnalyzeAsync(
        string recordingPath,
        CancellationToken cancellationToken = default)
    {
        var result = await AnalyzeDetailsAsync(recordingPath, cancellationToken).ConfigureAwait(false);
        return RecordingPacketInspectionFormatter.Format(result);
    }

    public static async Task<RecordingPacketInspectionResult> AnalyzeDetailsAsync(
        string recordingPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            return RecordingPacketInspectionResult.Unavailable("Packet histogram unavailable: recording path is missing.");
        }

        var loadResult = await TelemetryRecordingFile.LoadAsync(recordingPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!loadResult.Succeeded || loadResult.Recording is null)
        {
            return RecordingPacketInspectionResult.Unavailable($"Packet histogram unavailable: {loadResult.Message}");
        }

        var recording = loadResult.Recording;
        if (!string.Equals(recording.Metadata.SourceGame, "F1 25", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingPacketInspectionResult.UnsupportedSourceGame(
                $"Packet histogram unavailable: source game {recording.Metadata.SourceGame} is not supported.");
        }

        var countsById = new Dictionary<byte, long>();
        var previewEntries = new List<RecordingPacketPreviewEntry>();
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
                TryAddPreviewEntry(
                    previewEntries,
                    new RecordingPacketPreviewEntry(
                        packet.SequenceNumber,
                        packet.RelativeTime,
                        $"{parseResult.Definition.Name}#{parseResult.Definition.Id}",
                        packet.Payload.Length));
            }
            else if (parseResult.WasIgnored)
            {
                ignoredCount++;
                TryAddPreviewEntry(
                    previewEntries,
                    new RecordingPacketPreviewEntry(
                        packet.SequenceNumber,
                        packet.RelativeTime,
                        $"unknown packet ID {parseResult.Header?.PacketId.ToString() ?? "n/a"}",
                        packet.Payload.Length));
            }
            else
            {
                failedCount++;
                TryAddPreviewEntry(
                    previewEntries,
                    new RecordingPacketPreviewEntry(
                        packet.SequenceNumber,
                        packet.RelativeTime,
                        "invalid header",
                        packet.Payload.Length));
            }
        }

        var histogramEntries = countsById
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .Select(item =>
            {
                F125PacketDefinitions.TryGetById(item.Key, out var definition);
                var name = definition?.Name ?? $"Packet {item.Key}";
                return new RecordingPacketHistogramEntry(name, item.Key, item.Value);
            })
            .ToList();

        return RecordingPacketInspectionResult.Success(
            new RecordingPacketInspectionAnalysis(
                recording.Metadata.SourceGame,
                recording.Packets.Count,
                histogramEntries,
                ignoredCount,
                failedCount,
                previewEntries));
    }

    private static void TryAddPreviewEntry(List<RecordingPacketPreviewEntry> previewEntries, RecordingPacketPreviewEntry entry)
    {
        if (previewEntries.Count >= 5)
        {
            return;
        }

        previewEntries.Add(entry);
    }
}
