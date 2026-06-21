using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowTelemetryTests
{
    [Fact]
    public void PacketReceivedHandlerOnlyEnqueues()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("_telemetrySessionController.Enqueue(_telemetryIngressWorker, e.Packet);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleLiveTelemetryPacketAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hapticPipeline.OfferLiveTelemetryPacketAsync", source, StringComparison.Ordinal);
    }
}
