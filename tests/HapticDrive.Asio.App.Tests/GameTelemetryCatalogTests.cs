using HapticDrive.Asio.App;
using HapticDrive.Asio.Telemetry.F1_25;

namespace HapticDrive.Asio.App.Tests;

public sealed class GameTelemetryCatalogTests
{
    [Fact]
    public void NormalizeGameId_FallsBackToDefaultForUnknownValues()
    {
        Assert.Equal(GameTelemetryCatalog.DefaultGameId, GameTelemetryCatalog.NormalizeGameId(null));
        Assert.Equal(GameTelemetryCatalog.DefaultGameId, GameTelemetryCatalog.NormalizeGameId("unknown-game"));
    }

    [Fact]
    public void CreateAdapter_ReturnsF125AdapterForDefaultGame()
    {
        var adapter = GameTelemetryCatalog.CreateAdapter(GameTelemetryCatalog.DefaultGameId);

        Assert.IsType<F125GameTelemetryAdapter>(adapter);
        Assert.Equal("F1 25", adapter.GameName);
    }
}
