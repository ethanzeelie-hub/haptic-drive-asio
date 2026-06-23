using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Core.Tests;

public sealed class HapticFrameTests
{
    [Fact]
    public void HapticFrame_UsesTypedSignalStampsInsteadOfFreshnessDictionary()
    {
        var signalStampsProperty = typeof(HapticFrame).GetProperty(nameof(HapticFrame.SignalStamps));

        Assert.NotNull(signalStampsProperty);
        Assert.Equal(typeof(HapticFrameSignalStamps), signalStampsProperty!.PropertyType);
        Assert.Null(typeof(HapticFrame).GetProperty("Freshness"));

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.Core",
            "Haptics",
            "HapticFrame.cs"));
        Assert.DoesNotContain("IReadOnlyDictionary<string, VehicleSignalFreshness>", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HapticDrive.Asio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
