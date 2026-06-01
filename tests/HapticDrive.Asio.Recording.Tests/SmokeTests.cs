namespace HapticDrive.Asio.Recording.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void RecordingAssemblyIsReferenceable()
    {
        Assert.Equal("HapticDrive.Asio.Recording", typeof(global::HapticDrive.Asio.Recording.AssemblyMarker).Namespace);
    }
}
