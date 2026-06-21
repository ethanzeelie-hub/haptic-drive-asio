using System.Text;

namespace HapticDrive.Asio.Core.Persistence;

public static class AtomicFileWriter
{
    public static void WriteAllText(
        string path,
        string contents,
        Encoding? encoding = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        Write(
            path,
            stream =>
            {
                using var writer = new StreamWriter(
                    stream,
                    encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 16 * 1024,
                    leaveOpen: true);
                writer.Write(contents);
                writer.Flush();
            });
    }

    public static void Write(
        string path,
        Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);

        var fullPath = PrepareDestination(path);
        var tempPath = CreateTempPath(fullPath);
        var backupPath = tempPath + ".bak";

        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 16 * 1024))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            ReplaceTempFile(fullPath, tempPath, backupPath);
        }
        catch
        {
            DeleteIfExists(tempPath);
            DeleteIfExists(backupPath);
            throw;
        }
    }

    public static async ValueTask WriteAsync(
        string path,
        Func<Stream, CancellationToken, ValueTask> writeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(writeAsync);

        var fullPath = PrepareDestination(path);
        var tempPath = CreateTempPath(fullPath);
        var backupPath = tempPath + ".bak";

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 16 * 1024,
                             useAsync: true))
            {
                await writeAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            ReplaceTempFile(fullPath, tempPath, backupPath);
        }
        catch
        {
            DeleteIfExists(tempPath);
            DeleteIfExists(backupPath);
            throw;
        }
    }

    private static string PrepareDestination(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }

    private static string CreateTempPath(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Atomic file writes require a destination directory.");

        return Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static void ReplaceTempFile(
        string fullPath,
        string tempPath,
        string backupPath)
    {
        if (File.Exists(fullPath))
        {
            File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
            DeleteIfExists(backupPath);
            return;
        }

        File.Move(tempPath, fullPath);
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
