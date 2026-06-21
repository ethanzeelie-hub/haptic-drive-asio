using HapticDrive.Asio.App;
using HapticDrive.Asio.Core.Games;

namespace HapticDrive.Asio.App.Tests;

public sealed class GameIntegrationRegistryTests
{
    [Fact]
    public void DefaultIntegrationIsF125()
    {
        var descriptor = GameTelemetryCatalog.Registry.Default;

        Assert.Equal("f1-25", descriptor.Id.Value);
        Assert.Equal("F1 25", descriptor.DisplayName);
    }

    [Fact]
    public void F125DescriptorUsesV3Protocol()
    {
        var descriptor = GameTelemetryCatalog.Registry.GetRequired(new GameIntegrationId("f1-25"));

        Assert.Equal("F1 25 UDP", descriptor.TelemetryProtocolName);
        Assert.Equal("v3", descriptor.TelemetryProtocolVersion);
        Assert.Equal(20778, descriptor.EndpointDefaults.UdpPort);
        Assert.False(descriptor.EndpointDefaults.AllowLanTelemetry);
    }

    [Fact]
    public void UnknownGameIdThrowsClearException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GameTelemetryCatalog.Registry.GetRequired(new GameIntegrationId("unknown-game")));

        Assert.Contains("unknown-game", ex.Message, StringComparison.Ordinal);
    }
}
