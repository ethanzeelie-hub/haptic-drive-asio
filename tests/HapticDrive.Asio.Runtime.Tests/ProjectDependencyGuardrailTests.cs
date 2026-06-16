using System.Reflection;
using System.Xml.Linq;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class ProjectDependencyGuardrailTests
{
    [Fact]
    public void RuntimeAssembly_DoesNotReferenceAppAssembly()
    {
        var references = typeof(HapticDrive.Asio.Runtime.AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("HapticDrive.Asio.App", references, StringComparer.Ordinal);
    }

    [Fact]
    public void ProductionProjectReferences_KeepCurrentRuntimeOwnershipDirectionExplicit()
    {
        var graph = LoadProductionProjectGraph();

        Assert.DoesNotContain("HapticDrive.Asio.App", graph["HapticDrive.Asio.Runtime"]);
        Assert.DoesNotContain("HapticDrive.Actuation", graph["HapticDrive.Asio.Runtime"]);
        Assert.DoesNotContain("HapticDrive.Asio.App", graph["HapticDrive.Simagic.PHPR.Abstractions"]);
        Assert.DoesNotContain("HapticDrive.Asio.App", graph["HapticDrive.Simagic.PHPR.Output.Windows"]);
        Assert.Contains("HapticDrive.Asio.Runtime", graph["HapticDrive.Actuation"]);
        Assert.Contains("HapticDrive.Asio.Runtime", graph["HapticDrive.Asio.App"]);
        Assert.Contains("HapticDrive.Actuation", graph["HapticDrive.Asio.App"]);
        Assert.Contains("HapticDrive.Simagic.PHPR.Abstractions", graph["HapticDrive.Actuation"]);
        Assert.Contains("HapticDrive.Simagic.PHPR.Output.Windows", graph["HapticDrive.Asio.App"]);
    }

    [Fact]
    public void ProductionProjectReferenceGraph_HasNoCycles()
    {
        var graph = LoadProductionProjectGraph();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();

        foreach (var projectName in graph.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (DetectCycle(projectName, graph, visited, visiting, stack, out var cycle))
            {
                Assert.Fail($"Production project reference cycle detected: {cycle}");
            }
        }
    }

    private static bool DetectCycle(
        string projectName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> graph,
        HashSet<string> visited,
        HashSet<string> visiting,
        List<string> stack,
        out string cycle)
    {
        if (visiting.Contains(projectName))
        {
            var startIndex = stack.FindIndex(name => string.Equals(name, projectName, StringComparison.OrdinalIgnoreCase));
            var cycleNodes = startIndex >= 0
                ? stack.Skip(startIndex).Concat([projectName])
                : stack.Concat([projectName]);
            cycle = string.Join(" -> ", cycleNodes);
            return true;
        }

        if (!visited.Add(projectName))
        {
            cycle = string.Empty;
            return false;
        }

        visiting.Add(projectName);
        stack.Add(projectName);

        foreach (var dependency in graph[projectName])
        {
            if (!graph.ContainsKey(dependency))
            {
                continue;
            }

            if (DetectCycle(dependency, graph, visited, visiting, stack, out cycle))
            {
                return true;
            }
        }

        stack.RemoveAt(stack.Count - 1);
        visiting.Remove(projectName);
        cycle = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadProductionProjectGraph()
    {
        var srcDirectory = Path.Combine(FindRepositoryRoot(), "src");
        return Directory.GetFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                LoadProjectReferences,
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> LoadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;

        return document.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FindRepositoryRoot()
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
}
