using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowCompositionGuardrailTests
{
    [Fact]
    public void NoGetRequiredControlRemains()
    {
        var appSourceDirectory = Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App");

        foreach (var path in Directory.GetFiles(appSourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("GetRequiredControl", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FindName(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ControllersDoNotReachIntoViewsByControlName()
    {
        var controllersDirectory = Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "Controllers");

        foreach (var path in Directory.GetFiles(controllersDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var source = File.ReadAllText(path);

            Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Windows.Controls", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ComboBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ListBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ItemsControl", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FindName(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ViewControl.", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MainWindowDoesNotOwnHidWriterOrPipelineCoordinatorDirectly()
    {
        var appSourceDirectory = Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App");

        foreach (var path in Directory.GetFiles(appSourceDirectory, "MainWindow*.cs", SearchOption.TopDirectoryOnly))
        {
            var source = File.ReadAllText(path);

            Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DeferredWindowsHidReportWriter", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AsioAudioOutputDevice", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HapticPipelineCoordinator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PaddleInputRoutingCoordinator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_outputInterlock.Trip(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_hapticPipeline", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_phprDirectRuntime", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".StartAsync(", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".StopAsync(", source, StringComparison.Ordinal);
        }
    }
}
