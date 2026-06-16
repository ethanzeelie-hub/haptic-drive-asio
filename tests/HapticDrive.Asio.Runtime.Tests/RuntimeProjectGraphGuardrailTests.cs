using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class RuntimeProjectGraphGuardrailTests
{
    [Fact]
    public void RuntimeAssemblyDoesNotReferenceActuation()
    {
        var references = typeof(HapticPipelineSnapshot).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("HapticDrive.Actuation", references, StringComparer.Ordinal);
        Assert.DoesNotContain("HapticDrive.Asio.App", references, StringComparer.Ordinal);
    }
}
