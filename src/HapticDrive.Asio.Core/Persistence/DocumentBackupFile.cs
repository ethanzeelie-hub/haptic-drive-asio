namespace HapticDrive.Asio.Core.Persistence;

public static class DocumentBackupFile
{
    public static string GetBackupPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.GetFullPath(path) + ".lastgood";
    }

    public static bool TryRefreshFromPrimary(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            File.Copy(fullPath, GetBackupPath(fullPath), overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }
}
