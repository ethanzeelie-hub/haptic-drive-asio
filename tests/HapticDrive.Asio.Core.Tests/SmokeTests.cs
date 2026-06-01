namespace HapticDrive.Asio.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void CoreAssemblyIsReferenceable()
    {
        Assert.Equal("HapticDrive.Asio.Core", typeof(global::HapticDrive.Asio.Core.AssemblyMarker).Namespace);
    }
}
