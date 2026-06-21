using System.Text;
using HapticDrive.Asio.Core.Persistence;

namespace HapticDrive.Asio.Core.Tests;

public sealed class AtomicFileWriterAsyncTests
{
    [Fact]
    public async Task AsyncWriteFlushesAndReplacesAtomically()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "profile.json");
        await File.WriteAllTextAsync(path, "before");
        var observedOriginalDuringWrite = false;

        await AtomicFileWriter.WriteAsync(
            path,
            async (stream, cancellationToken) =>
            {
                await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
                await writer.WriteAsync("after");
                await writer.FlushAsync(cancellationToken);
                observedOriginalDuringWrite = await File.ReadAllTextAsync(path, cancellationToken) == "before";
            });

        Assert.True(observedOriginalDuringWrite);
        Assert.Equal("after", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp", SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.bak", SearchOption.TopDirectoryOnly));
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
