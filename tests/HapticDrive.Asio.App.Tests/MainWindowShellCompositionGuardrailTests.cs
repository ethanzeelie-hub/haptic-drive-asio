using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowShellCompositionGuardrailTests
{
    [Fact]
    public void MainWindowXaml_RemainsShellHostForAllExtractedViews()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));

        Assert.Contains("DashboardViewControl", source, StringComparison.Ordinal);
        Assert.Contains("DevicesViewControl", source, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl", source, StringComparison.Ordinal);
        Assert.Contains("RoutingMixerViewControl", source, StringComparison.Ordinal);
        Assert.Contains("TelemetryUdpViewControl", source, StringComparison.Ordinal);
        Assert.Contains("ProfilesViewControl", source, StringComparison.Ordinal);
        Assert.Contains("TestingValidationViewControl", source, StringComparison.Ordinal);
        Assert.Contains("AdvancedDiagnosticsViewControl", source, StringComparison.Ordinal);
        Assert.Contains("NavigationList", source, StringComparison.Ordinal);
        Assert.Contains("PageStatusText", source, StringComparison.Ordinal);
        Assert.Contains("FooterStatusText", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXaml_DoesNotReabsorbExtractedPageLayoutMarkers()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml"));

        Assert.DoesNotContain("DashboardWorkflowStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputModeComboBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MasterGainSlider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordingLibraryListBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileNameTextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ManualBst1StrengthTextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RealPhprCandidateComboBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Ready Checklist", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic Test Bench", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Runtime Diagnostics", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractedViews_KeepTheirWorkflowBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = ReadView(repositoryRoot, "DashboardView.xaml");
        var devices = ReadView(repositoryRoot, "DevicesView.xaml");
        var effects = ReadView(repositoryRoot, "EffectsView.xaml");
        var routing = ReadView(repositoryRoot, "RoutingMixerView.xaml");
        var telemetry = ReadView(repositoryRoot, "TelemetryUdpView.xaml");
        var profiles = ReadView(repositoryRoot, "ProfilesView.xaml");
        var testing = ReadView(repositoryRoot, "TestingValidationView.xaml");
        var advanced = ReadView(repositoryRoot, "AdvancedDiagnosticsView.xaml");

        Assert.Contains("Ready Checklist", dashboard, StringComparison.Ordinal);
        Assert.Contains("Simagic Wheel / Shift Paddles", devices, StringComparison.Ordinal);
        Assert.Contains("BST-1 Seat Shaker", effects, StringComparison.Ordinal);
        Assert.Contains("Output Routing Summary", routing, StringComparison.Ordinal);
        Assert.Contains("Recording And Replay", telemetry, StringComparison.Ordinal);
        Assert.Contains("Profile name", profiles, StringComparison.Ordinal);
        Assert.Contains("Synthetic Test Bench", testing, StringComparison.Ordinal);
        Assert.Contains("Runtime Diagnostics", advanced, StringComparison.Ordinal);
        Assert.Contains("Export Support Bundle", advanced, StringComparison.Ordinal);
        Assert.Contains("Copy Report", advanced, StringComparison.Ordinal);
        Assert.Contains("Report ID", advanced, StringComparison.Ordinal);
        Assert.Contains("Candidate", advanced, StringComparison.Ordinal);

        var normalViews = string.Concat(dashboard, devices, effects, routing, telemetry, profiles, testing);
        Assert.DoesNotContain("RealPhprCandidateComboBox", normalViews, StringComparison.Ordinal);
        Assert.DoesNotContain("Report ID", normalViews, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy Report", normalViews, StringComparison.Ordinal);
        Assert.DoesNotContain("Export Support Bundle", normalViews, StringComparison.Ordinal);
        Assert.DoesNotContain("Runtime Diagnostics", normalViews, StringComparison.Ordinal);
    }

    private static string ReadView(string repositoryRoot, string fileName)
    {
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HapticDrive.Asio.App",
            "Views",
            fileName));
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
