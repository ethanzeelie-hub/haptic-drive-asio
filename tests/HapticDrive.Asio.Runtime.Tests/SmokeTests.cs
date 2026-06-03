namespace HapticDrive.Asio.Runtime.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void RuntimeTests_ProjectLoads()
    {
        Assert.NotNull(typeof(HapticDrive.Asio.Runtime.AssemblyMarker).Assembly);
    }
}

