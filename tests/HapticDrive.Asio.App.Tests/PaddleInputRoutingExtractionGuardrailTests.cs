using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class PaddleInputRoutingExtractionGuardrailTests
{
    [Fact]
    public void PaddleRoutingCoordinatorLivesInAppAssemblyButNotInMainWindow()
    {
        Assert.Equal("HapticDrive.Asio.App", typeof(PaddleInputRoutingCoordinator).Assembly.GetName().Name);
        Assert.NotEqual(nameof(MainWindow), typeof(PaddleInputRoutingCoordinator).Name);
    }

    [Fact]
    public void PaddleRoutingCoordinatorSource_HasNoWpfOrNamedControlReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "PaddleInputRoutingCoordinator.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FooterStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShiftIntentStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PaddleGearBenchStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RealPhprDirectStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhprValidationStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MockGearPulseStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MockPedalEffectsStatusText", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_NoLongerDeclaresPaddleRoutingBodyHelpers()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("_paddleInputRoutingCoordinator.HandleAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<string> RoutePaddleGearBenchAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<string> RoutePaddleGearBenchMockAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<string> RouteBst1PaddleGearBenchAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<string> RoutePaddleGearBenchDirectAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task HandlePaddleInputEventExceptionAsync(", source, StringComparison.Ordinal);
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