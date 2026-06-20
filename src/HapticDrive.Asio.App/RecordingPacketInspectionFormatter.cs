namespace HapticDrive.Asio.App;

internal static class RecordingPacketInspectionFormatter
{
    public static string Format(RecordingPacketInspectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Succeeded || result.Analysis is null)
        {
            return result.Message;
        }

        if (result.Analysis.PacketCount == 0)
        {
            return "Packet histogram: no packets in recording.";
        }

        var entries = result.Analysis.HistogramEntries
            .Select(entry => $"{entry.Name}#{entry.PacketId}: {entry.Count:N0}")
            .ToList();

        if (result.Analysis.IgnoredUnknownPacketCount > 0)
        {
            entries.Add($"Ignored unknown packet IDs: {result.Analysis.IgnoredUnknownPacketCount:N0}");
        }

        if (result.Analysis.InvalidHeaderCount > 0)
        {
            entries.Add($"Invalid packet headers: {result.Analysis.InvalidHeaderCount:N0}");
        }

        var histogramText = $"Packet histogram: {string.Join("; ", entries)}.";
        if (result.Analysis.PreviewEntries.Count == 0)
        {
            return histogramText;
        }

        var previewText = string.Join(
            " | ",
            result.Analysis.PreviewEntries.Select(entry =>
                $"seq {entry.SequenceNumber:N0}; {FormatRelativeTime(entry.RelativeTime)}; {entry.Label}; {entry.PayloadSizeBytes:N0} B"));
        return $"{histogramText}{Environment.NewLine}{Environment.NewLine}Packet preview: {previewText}.";
    }

    private static string FormatRelativeTime(TimeSpan relativeTime)
    {
        return relativeTime < TimeSpan.FromSeconds(1)
            ? $"{relativeTime.TotalMilliseconds:0} ms"
            : relativeTime.ToString(@"hh\:mm\:ss\.fff");
    }
}
