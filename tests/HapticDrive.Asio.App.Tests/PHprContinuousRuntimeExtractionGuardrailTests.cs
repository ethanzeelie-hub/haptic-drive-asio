using System.IO;
using HapticDrive.Actuation.PHpr;

namespace HapticDrive.Asio.App.Tests;

public sealed class PHprContinuousRuntimeExtractionGuardrailTests
{
    [Fact]
    public void ContinuousEffectsCoordinatorLivesOutsideAppAssembly()
    {
        Assert.Equal("HapticDrive.Actuation", typeof(PHprContinuousEffectsRuntimeCoordinator).Assembly.GetName().Name);
        Assert.NotEqual("HapticDrive.Asio.App", typeof(PHprContinuousEffectsRuntimeCoordinator).Assembly.GetName().Name);
    }

    [Fact]
    public void MainWindowSource_NoLongerDeclaresContinuousRuntimeLoopBodies()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "HapticDrive.Asio.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("private void StartRealSlipLockRuntime()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task RunRealSlipLockRuntimeAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private void StartRealRoadVibrationRuntime()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task RunRealRoadVibrationRuntimeAsync(", source, StringComparison.Ordinal);
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
