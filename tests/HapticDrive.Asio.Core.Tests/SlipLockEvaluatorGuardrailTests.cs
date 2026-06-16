using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Core.Tests;

public sealed class SlipLockEvaluatorGuardrailTests
{
    [Fact]
    public void CoreAssemblyAndSharedEvaluatorHaveNoUiOrOutputDependencies()
    {
        var assembly = typeof(SlipLockEvaluator).Assembly;
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Equal("HapticDrive.Asio.Core", assembly.GetName().Name);
        Assert.DoesNotContain("HapticDrive.Asio.App", references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Asio.Audio", references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Actuation", references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Asio.Runtime", references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Simagic.PHPR.Output.Windows", references, StringComparer.Ordinal);
        Assert.DoesNotContain("PresentationFramework", references, StringComparer.Ordinal);
        Assert.DoesNotContain("PresentationCore", references, StringComparer.Ordinal);
        Assert.DoesNotContain("WindowsBase", references, StringComparer.Ordinal);
        Assert.DoesNotContain("NAudio.Asio", references, StringComparer.Ordinal);
    }

    [Fact]
    public void EvaluatorSurfaceDoesNotExposeAudioOrHidOutputTypes()
    {
        var parameterTypeNames = typeof(SlipLockEvaluator)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .Concat(
                typeof(SlipLockEvaluator)
                    .GetMethods()
                    .SelectMany(method => method.GetParameters())
                    .Select(parameter => parameter.ParameterType.Name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.DoesNotContain("IAudioOutputDevice", parameterTypeNames);
        Assert.DoesNotContain("IPHprOutputDevice", parameterTypeNames);
        Assert.DoesNotContain("PHprCommand", parameterTypeNames);
        Assert.DoesNotContain("AsioOut", parameterTypeNames);
    }
}
