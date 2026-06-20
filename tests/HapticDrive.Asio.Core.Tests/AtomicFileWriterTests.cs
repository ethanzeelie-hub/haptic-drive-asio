using System.Text;
using HapticDrive.Asio.Core.Persistence;

namespace HapticDrive.Asio.Core.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public void Write_PreservesExistingFileWhenWriterThrows()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "original");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AtomicFileWriter.Write(
                path,
                stream =>
                {
                    using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
                    writer.Write("partial");
                    writer.Flush();
                    throw new InvalidOperationException("boom");
                }));

        Assert.Equal("boom", exception.Message);
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task WriteAsync_ReplacesExistingFileAndLeavesNoBackupOrTempFiles()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "profile.json");
        await File.WriteAllTextAsync(path, "before");

        await AtomicFileWriter.WriteAsync(
            path,
            async (stream, cancellationToken) =>
            {
                await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
                await writer.WriteAsync("after");
                await writer.FlushAsync(cancellationToken);
            });

        Assert.Equal("after", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp", SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.bak", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void DocumentBackupFile_RefreshesLastKnownGoodCopy()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        var backupPath = DocumentBackupFile.GetBackupPath(path);
        File.WriteAllText(path, "stable");

        var refreshed = DocumentBackupFile.TryRefreshFromPrimary(path);

        Assert.True(refreshed);
        Assert.True(File.Exists(backupPath));
        Assert.Equal("stable", File.ReadAllText(backupPath));
    }

    [Fact]
    public void DocumentBackupHistory_RefreshesRetainedHistoryAndPrunesOlderSnapshots()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "settings.json");

        for (var index = 1; index <= 5; index++)
        {
            File.WriteAllText(path, $"stable-{index}");
            Assert.True(DocumentBackupHistory.TryRefreshFromPrimary(path, maxRetained: 3));
            Thread.Sleep(20);
        }

        var historyDirectory = DocumentBackupHistory.GetHistoryDirectoryPath(path);
        var historyPaths = DocumentBackupHistory.GetHistoryPathsNewestFirst(path);

        Assert.True(Directory.Exists(historyDirectory));
        Assert.Equal(3, historyPaths.Count);
        Assert.All(historyPaths, historyPath => Assert.True(File.Exists(historyPath)));
        Assert.Equal("stable-5", File.ReadAllText(historyPaths[0]));
        Assert.Equal("stable-4", File.ReadAllText(historyPaths[1]));
        Assert.Equal("stable-3", File.ReadAllText(historyPaths[2]));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "HapticDrive.Asio.Core.Tests",
            Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
