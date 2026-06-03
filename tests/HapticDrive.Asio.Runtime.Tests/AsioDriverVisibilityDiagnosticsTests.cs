using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class AsioDriverVisibilityDiagnosticsTests
{
    [Fact]
    public async Task VisibilityDiagnostics_FindFakeMTrackDriverWithoutHardware()
    {
        var diagnostics = new AsioDriverVisibilityDiagnostics(
            new FakeAsioDriverCatalog(["Other Driver", "M-Audio M-Track Solo and Duo ASIO"]));

        var snapshot = await diagnostics.RefreshAsync();

        Assert.True(snapshot.Succeeded);
        Assert.True(snapshot.IsMTrackDriverVisible);
        Assert.Equal("M-Audio M-Track Solo and Duo ASIO", snapshot.MatchedMTrackDriverName);
    }

    [Fact]
    public async Task VisibilityDiagnostics_ReportMissingDriverGracefully()
    {
        var diagnostics = new AsioDriverVisibilityDiagnostics(new FakeAsioDriverCatalog([]));

        var snapshot = await diagnostics.RefreshAsync();

        Assert.True(snapshot.Succeeded);
        Assert.False(snapshot.IsMTrackDriverVisible);
        Assert.Empty(snapshot.DriverNames);
    }

    private sealed class FakeAsioDriverCatalog : IAsioDriverCatalog
    {
        private readonly IReadOnlyList<string> _drivers;

        public FakeAsioDriverCatalog(IReadOnlyList<string> drivers)
        {
            _drivers = drivers;
        }

        public ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_drivers);
        }
    }
}

