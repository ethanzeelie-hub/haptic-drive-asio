namespace HapticDrive.Asio.Audio.Tests;

public sealed class EffectInputGuardrailTests
{
    [Fact]
    public void EffectsDoNotReadRawF125SurfaceIdsOrDriverEnums()
    {
        var effectsDirectory = Path.Combine(FindRepositoryRoot(), "src", "HapticDrive.Asio.Audio", "Effects");
        var offendingFiles = Directory
            .GetFiles(effectsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("Legacy", StringComparison.Ordinal))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("SurfaceTypeIds", StringComparison.Ordinal)
                    || source.Contains("DriverStatus", StringComparison.Ordinal)
                    || source.Contains("PitStatus", StringComparison.Ordinal)
                    || source.Contains("SessionType", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offendingFiles);
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
