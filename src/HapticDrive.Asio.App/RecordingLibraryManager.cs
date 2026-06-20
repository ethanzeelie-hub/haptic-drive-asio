using System.IO;
using HapticDrive.Asio.Recording;

namespace HapticDrive.Asio.App;

internal enum RecordingLibraryDeleteStatus
{
    Success,
    Missing,
    Blocked,
    Failure
}

internal sealed record RecordingLibraryDeleteResult(
    RecordingLibraryDeleteStatus Status,
    string Message)
{
    public bool Succeeded => Status is RecordingLibraryDeleteStatus.Success or RecordingLibraryDeleteStatus.Missing;

    public static RecordingLibraryDeleteResult Success(string message)
    {
        return new(RecordingLibraryDeleteStatus.Success, message);
    }

    public static RecordingLibraryDeleteResult Missing(string message)
    {
        return new(RecordingLibraryDeleteStatus.Missing, message);
    }

    public static RecordingLibraryDeleteResult Blocked(string message)
    {
        return new(RecordingLibraryDeleteStatus.Blocked, message);
    }

    public static RecordingLibraryDeleteResult Failure(string message)
    {
        return new(RecordingLibraryDeleteStatus.Failure, message);
    }
}

internal enum RecordingLibraryRenameStatus
{
    Success,
    Missing,
    Blocked,
    Failure
}

internal sealed record RecordingLibraryRenameResult(
    RecordingLibraryRenameStatus Status,
    string Message,
    string? RenamedPath = null)
{
    public bool Succeeded => Status is RecordingLibraryRenameStatus.Success or RecordingLibraryRenameStatus.Missing;

    public static RecordingLibraryRenameResult Success(string message, string renamedPath)
    {
        return new(RecordingLibraryRenameStatus.Success, message, renamedPath);
    }

    public static RecordingLibraryRenameResult Missing(string message)
    {
        return new(RecordingLibraryRenameStatus.Missing, message);
    }

    public static RecordingLibraryRenameResult Blocked(string message)
    {
        return new(RecordingLibraryRenameStatus.Blocked, message);
    }

    public static RecordingLibraryRenameResult Failure(string message)
    {
        return new(RecordingLibraryRenameStatus.Failure, message);
    }
}

internal sealed record RecordingLibraryItem(
    string Path,
    string DisplayText,
    string DetailText,
    string SearchText);

internal static class RecordingLibraryDetailFormatter
{
    public static string BuildDetailText(string baseDetailText, string? analysisText = null)
    {
        if (string.IsNullOrWhiteSpace(analysisText))
        {
            return baseDetailText;
        }

        return $"{baseDetailText}{Environment.NewLine}{Environment.NewLine}{analysisText}";
    }
}

internal static class RecordingLibraryManager
{
    public static async Task<List<RecordingLibraryItem>> LoadAsync(
        string recordingsDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(recordingsDirectory))
        {
            return [];
        }

        var items = new List<RecordingLibraryItem>();
        foreach (var path in Directory
            .EnumerateFiles(recordingsDirectory, "*.hdrec", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await TelemetryRecordingFile.LoadSummaryAsync(path, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded && result.Summary is not null)
            {
                var summary = result.Summary;
                var sizeText = FormatByteSize(summary.FileSizeBytes);
                var createdLocal = summary.Metadata.CreatedAtUtc.ToLocalTime();
                var payloadText = FormatByteSize(summary.PayloadBytes);
                var durationText = FormatDuration(summary.Duration);
                var sequenceRangeText = FormatSequenceRange(summary);
                var packetRateText = FormatPacketRate(summary.ApproximatePacketRateHz);
                var sequenceHealthText = summary.MissingSequenceCount == 0
                    ? "sequence continuous"
                    : $"sequence gaps {summary.MissingSequenceCount:N0} (largest {summary.LargestSequenceGap:N0})";
                var detailText =
                    $"Created {createdLocal:g}; duration {durationText}; payload {payloadText}; {sequenceRangeText}; {packetRateText}; {sequenceHealthText}; source {summary.Metadata.SourceGame}; profile {summary.Metadata.SourceProfile}; app {summary.Metadata.AppVersion}; modified {summary.LastModifiedAtUtc.ToLocalTime():g}.";
                items.Add(new RecordingLibraryItem(
                    path,
                    $"{Path.GetFileName(path)} - {summary.PacketCount:N0} packet(s) - {durationText} - {sizeText}",
                    detailText,
                    BuildSearchText(
                        Path.GetFileName(path),
                        summary,
                        detailText,
                        sequenceHealthText,
                        sequenceRangeText)));
                continue;
            }

            items.Add(new RecordingLibraryItem(
                path,
                $"{Path.GetFileName(path)} - {result.Status}",
                result.Message,
                $"{Path.GetFileName(path)} {result.Status} {result.Message}"));
        }

        return items;
    }

    public static List<RecordingLibraryItem> Filter(
        IReadOnlyList<RecordingLibraryItem> items,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(items);

        var terms = (query ?? string.Empty)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return items.ToList();
        }

        return items
            .Where(item => terms.All(term => item.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static string? FindLatestRecordingPath(string recordingsDirectory)
    {
        if (!Directory.Exists(recordingsDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(recordingsDirectory, "*.hdrec", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static RecordingLibraryDeleteResult DeleteSelected(
        string recordingsDirectory,
        string? selectedPath,
        string? activeRecordingPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return RecordingLibraryDeleteResult.Blocked("Select a recording before deleting.");
        }

        string fullDirectory;
        string fullPath;
        try
        {
            fullDirectory = Path.GetFullPath(recordingsDirectory);
            fullPath = Path.GetFullPath(selectedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RecordingLibraryDeleteResult.Blocked($"Delete blocked: recording path is invalid: {ex.Message}");
        }

        if (!IsPathInsideDirectory(fullDirectory, fullPath))
        {
            return RecordingLibraryDeleteResult.Blocked("Delete blocked: selected file is outside the recordings folder.");
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".hdrec", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingLibraryDeleteResult.Blocked("Delete blocked: selected file is not an .hdrec recording.");
        }

        if (!string.IsNullOrWhiteSpace(activeRecordingPath))
        {
            string activeFullPath;
            try
            {
                activeFullPath = Path.GetFullPath(activeRecordingPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return RecordingLibraryDeleteResult.Blocked($"Delete blocked: active recording path is invalid: {ex.Message}");
            }

            if (string.Equals(fullPath, activeFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return RecordingLibraryDeleteResult.Blocked("Delete blocked: the selected file is the active recording output.");
            }
        }

        if (!File.Exists(fullPath))
        {
            return RecordingLibraryDeleteResult.Missing("Selected recording was already missing; library refreshed.");
        }

        try
        {
            File.Delete(fullPath);
            return RecordingLibraryDeleteResult.Success($"Deleted recording {Path.GetFileName(fullPath)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return RecordingLibraryDeleteResult.Failure($"Recording could not be deleted: {ex.Message}");
        }
    }

    public static RecordingLibraryRenameResult RenameSelected(
        string recordingsDirectory,
        string? selectedPath,
        string? requestedName,
        string? activeRecordingPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return RecordingLibraryRenameResult.Blocked("Select a recording before renaming.");
        }

        string fullDirectory;
        string fullPath;
        try
        {
            fullDirectory = Path.GetFullPath(recordingsDirectory);
            fullPath = Path.GetFullPath(selectedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RecordingLibraryRenameResult.Blocked($"Rename blocked: recording path is invalid: {ex.Message}");
        }

        if (!IsPathInsideDirectory(fullDirectory, fullPath))
        {
            return RecordingLibraryRenameResult.Blocked("Rename blocked: selected file is outside the recordings folder.");
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".hdrec", StringComparison.OrdinalIgnoreCase))
        {
            return RecordingLibraryRenameResult.Blocked("Rename blocked: selected file is not an .hdrec recording.");
        }

        if (!string.IsNullOrWhiteSpace(activeRecordingPath))
        {
            string activeFullPath;
            try
            {
                activeFullPath = Path.GetFullPath(activeRecordingPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return RecordingLibraryRenameResult.Blocked($"Rename blocked: active recording path is invalid: {ex.Message}");
            }

            if (string.Equals(fullPath, activeFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return RecordingLibraryRenameResult.Blocked("Rename blocked: the selected file is the active recording output.");
            }
        }

        if (!File.Exists(fullPath))
        {
            return RecordingLibraryRenameResult.Missing("Selected recording was already missing; library refreshed.");
        }

        if (!TryBuildRenamedPath(fullDirectory, requestedName, out var renamedPath, out var message))
        {
            return RecordingLibraryRenameResult.Blocked(message);
        }

        if (string.Equals(fullPath, renamedPath, StringComparison.OrdinalIgnoreCase))
        {
            return RecordingLibraryRenameResult.Success("Recording name unchanged; library refreshed.", renamedPath);
        }

        if (File.Exists(renamedPath))
        {
            return RecordingLibraryRenameResult.Blocked($"Rename blocked: {Path.GetFileName(renamedPath)} already exists.");
        }

        try
        {
            File.Move(fullPath, renamedPath);
            return RecordingLibraryRenameResult.Success(
                $"Renamed recording to {Path.GetFileName(renamedPath)}.",
                renamedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return RecordingLibraryRenameResult.Failure($"Recording could not be renamed: {ex.Message}");
        }
    }

    private static bool IsPathInsideDirectory(string fullDirectory, string fullPath)
    {
        var directory = fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryBuildRenamedPath(
        string fullDirectory,
        string? requestedName,
        out string renamedPath,
        out string message)
    {
        renamedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            message = "Enter a recording name before renaming.";
            return false;
        }

        var trimmed = requestedName.Trim();
        if (Path.IsPathRooted(trimmed)
            || trimmed.Contains(Path.DirectorySeparatorChar)
            || trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            message = "Rename blocked: use a recording name only, not a path.";
            return false;
        }

        var baseName = Path.GetFileNameWithoutExtension(trimmed);
        var sanitizedBaseName = SanitizeFileName(baseName).Trim();
        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            message = "Rename blocked: the recording name is empty after sanitization.";
            return false;
        }

        renamedPath = Path.GetFullPath(Path.Combine(fullDirectory, $"{sanitizedBaseName}.hdrec"));
        if (!IsPathInsideDirectory(fullDirectory, renamedPath))
        {
            message = "Rename blocked: renamed file would leave the recordings folder.";
            return false;
        }

        message = $"Recording rename target ready: {Path.GetFileName(renamedPath)}.";
        return true;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new char[fileName.Length];
        for (var index = 0; index < fileName.Length; index++)
        {
            sanitized[index] = invalid.Contains(fileName[index]) ? '-' : fileName[index];
        }

        return new string(sanitized).Trim().TrimEnd('.');
    }

    private static string FormatByteSize(long byteCount)
    {
        if (byteCount >= 1024 * 1024)
        {
            return $"{byteCount / 1024d / 1024d:0.0} MB";
        }

        if (byteCount >= 1024)
        {
            return $"{byteCount / 1024d:0.0} KB";
        }

        return $"{byteCount:N0} B";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0 ms";
        }

        if (duration < TimeSpan.FromSeconds(1))
        {
            return $"{duration.TotalMilliseconds:0} ms";
        }

        return duration.ToString(@"hh\:mm\:ss\.fff");
    }

    private static string FormatSequenceRange(TelemetryRecordingSummary summary)
    {
        if (!summary.FirstSequenceNumber.HasValue || !summary.LastSequenceNumber.HasValue)
        {
            return "sequence range unavailable";
        }

        return $"sequence {summary.FirstSequenceNumber.Value:N0}-{summary.LastSequenceNumber.Value:N0}";
    }

    private static string FormatPacketRate(double approximatePacketRateHz)
    {
        if (approximatePacketRateHz <= 0d || double.IsNaN(approximatePacketRateHz) || double.IsInfinity(approximatePacketRateHz))
        {
            return "packet rate n/a";
        }

        return $"approx {approximatePacketRateHz:0.0} pkt/s";
    }

    private static string BuildSearchText(
        string fileName,
        TelemetryRecordingSummary summary,
        string detailText,
        string sequenceHealthText,
        string sequenceRangeText)
    {
        return string.Join(
            ' ',
            [
                fileName,
                summary.Metadata.SourceGame,
                summary.Metadata.SourceProfile,
                summary.Metadata.AppVersion,
                summary.PacketCount.ToString("N0"),
                sequenceHealthText,
                sequenceRangeText,
                detailText
            ]);
    }
}
