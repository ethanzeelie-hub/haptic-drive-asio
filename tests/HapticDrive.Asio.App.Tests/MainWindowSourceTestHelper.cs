using System.IO;

namespace HapticDrive.Asio.App.Tests;

internal static class MainWindowSourceTestHelper
{
    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "HapticDrive.Asio.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing HapticDrive.Asio.sln.");
    }

    public static string ReadRepositoryFile(params string[] relativeSegments)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativeSegments]));
    }

    public static string ReadCombinedMainWindowSource()
    {
        var mainWindowDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App");

        var paths = Directory.GetFiles(mainWindowDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                return fileName.StartsWith("MainWindow", StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith("AppRuntimeSession", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            paths.Select(File.ReadAllText));
    }

    public static int ReadMainWindowCodeBehindLineCount()
    {
        return File.ReadLines(Path.Combine(
                FindRepositoryRoot(),
                "src",
                "HapticDrive.Asio.App",
                "MainWindow.xaml.cs"))
            .Count();
    }

    public static IReadOnlyDictionary<string, int> ReadMainWindowPartialLineCounts()
    {
        var mainWindowDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App");

        return Directory.GetFiles(mainWindowDirectory, "MainWindow*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => Path.GetFileName(path)!,
                path => File.ReadLines(path).Count(),
                StringComparer.OrdinalIgnoreCase);
    }
}
