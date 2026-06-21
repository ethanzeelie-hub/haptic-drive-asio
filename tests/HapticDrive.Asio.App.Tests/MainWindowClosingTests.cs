using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowClosingTests
{
    [Fact]
    public void ShutdownTimeoutDoesNotHangUiForever()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("var shutdownTimeout = TimeSpan.FromSeconds(5);", source, StringComparison.Ordinal);
        Assert.Contains("_ = ShutdownThenCloseAsync();", source, StringComparison.Ordinal);
        Assert.Contains("PerformBestEffortShutdownAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RunShutdownCleanupBlocking();", source, StringComparison.Ordinal);
    }
}
