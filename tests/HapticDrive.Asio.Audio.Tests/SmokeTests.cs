namespace HapticDrive.Asio.Audio.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void AudioAssemblyIsReferenceable()
    {
        Assert.Equal("HapticDrive.Asio.Audio", typeof(global::HapticDrive.Asio.Audio.AssemblyMarker).Namespace);
    }
}
