using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowTimerTests
{
    [Fact]
    public void TelemetryStatusTickIsSingleFlight()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("Interlocked.Exchange(ref _telemetryStatusTickInFlight, 1)", source, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Increment(ref _telemetryStatusTickSkippedCount)", source, StringComparison.Ordinal);
        Assert.Contains("Volatile.Write(ref _telemetryStatusTickInFlight, 0)", source, StringComparison.Ordinal);
    }
}