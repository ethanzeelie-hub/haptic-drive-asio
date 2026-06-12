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

internal sealed record RecordingLibraryItem(
    string Path,
    string DisplayText,
    string DetailText);

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
                var sizeText = summary.FileSizeBytes >= 1024 * 1024
                    ? $"{summary.FileSizeBytes / 1024d / 1024d:0.0} MB"
                    : $"{summary.FileSizeBytes / 1024d:0.0} KB";
                var createdLocal = summary.Metadata.CreatedAtUtc.ToLocalTime();
                items.Add(new RecordingLibraryItem(
                    path,
                    $"{Path.GetFileName(path)} - {summary.PacketCount:N0} packet(s) - {sizeText}",
                    $"Created {createdLocal:g}; source {summary.Metadata.SourceGame}; profile {summary.Metadata.SourceProfile}; app {summary.Metadata.AppVersion}; modified {summary.LastModifiedAtUtc.ToLocalTime():g}."));
                continue;
            }

            items.Add(new RecordingLibraryItem(
                path,
                $"{Path.GetFileName(path)} - {result.Status}",
                result.Message));
        }

        return items;
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

    private static bool IsPathInsideDirectory(string fullDirectory, string fullPath)
    {
        var directory = fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }
}
