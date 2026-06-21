using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowTelemetryTests
{
    [Fact]
    public void PacketReceivedHandlerOnlyEnqueues()
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

        Assert.Contains("_telemetryIngressWorker.Enqueue(e.Packet);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleLiveTelemetryPacketAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hapticPipeline.OfferLiveTelemetryPacketAsync", source, StringComparison.Ordinal);
    }
}
