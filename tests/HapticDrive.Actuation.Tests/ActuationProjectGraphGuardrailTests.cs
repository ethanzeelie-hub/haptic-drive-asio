using HapticDrive.Actuation.PHpr;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Actuation.Tests;

public sealed class ActuationProjectGraphGuardrailTests
{
    [Fact]
    public void ActuationAssemblyReferencesCoreEvaluatorAndNotRuntimeOrApp()
    {
        var runtimeAssemblyName = string.Concat("HapticDrive.Asio", ".Runtime");
        var references = typeof(PHprSlipLockRouter).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("HapticDrive.Asio.Core", references, StringComparer.Ordinal);
        Assert.DoesNotContain(runtimeAssemblyName, references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Asio.App", references, StringComparer.Ordinal);
        Assert.Equal("HapticDrive.Asio.Core", typeof(SlipLockEvaluator).Assembly.GetName().Name);
    }
}
