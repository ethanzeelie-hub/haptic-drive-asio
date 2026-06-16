using HapticDrive.Asio.Audio.Effects;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class AudioProjectGraphGuardrailTests
{
    [Fact]
    public void AudioAssemblyReferencesCoreButNotApp()
    {
        var references = typeof(SlipEffect).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("HapticDrive.Asio.Core", references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Asio.App", references, StringComparer.Ordinal);
    }
}
