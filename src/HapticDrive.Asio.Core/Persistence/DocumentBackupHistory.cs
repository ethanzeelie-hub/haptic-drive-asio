using System.Globalization;

namespace HapticDrive.Asio.Core.Persistence;

public static class DocumentBackupHistory
{
    public const int DefaultRetentionCount = 3;

    public static string GetHistoryDirectoryPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.GetFullPath(path) + ".history";
    }

    public static IReadOnlyList<string> GetHistoryPathsNewestFirst(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var historyDirectory = GetHistoryDirectoryPath(path);
        if (!Directory.Exists(historyDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(historyDirectory, "*.lastgood", SearchOption.TopDirectoryOnly)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryRefreshFromPrimary(string path, int maxRetained = DefaultRetentionCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (maxRetained <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetained), "Retained backup history count must be positive.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            var historyDirectory = GetHistoryDirectoryPath(fullPath);
            Directory.CreateDirectory(historyDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
            var historyPath = Path.Combine(historyDirectory, $"{timestamp}-{Guid.NewGuid():N}.lastgood");
            File.Copy(fullPath, historyPath, overwrite: false);

            var historyFiles = GetHistoryPathsNewestFirst(fullPath);
            foreach (var stalePath in historyFiles.Skip(maxRetained))
            {
                File.Delete(stalePath);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }
}
