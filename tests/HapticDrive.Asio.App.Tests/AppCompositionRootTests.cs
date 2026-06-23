using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime;

namespace HapticDrive.Asio.App.Tests;

public sealed class AppCompositionRootTests
{
    [Fact]
    public void Controllers_RunWithoutConstructingWpfWindow()
    {
        var root = new AppCompositionRoot();
        var services = root.Services;

        Assert.NotNull(services.EffectSettingsViewModel);
        Assert.IsType<ApplicationSafetyController>(services.ApplicationSafetyController);
        Assert.IsType<TelemetrySessionController>(services.TelemetrySessionController);
        Assert.IsType<AudioOutputController>(services.AudioOutputController);
        Assert.IsType<RecordingReplayController>(services.RecordingReplayController);
        Assert.IsType<PhprOutputController>(services.PhprOutputController);
        Assert.IsType<DiagnosticsPresentationController>(services.DiagnosticsPresentationController);
        Assert.IsType<AppSettingsStore>(services.SettingsStore);
        Assert.IsAssignableFrom<IOutputInterlock>(services.OutputInterlock);
        Assert.IsType<RuntimeLifecycleCoordinator>(services.RuntimeLifecycleCoordinator);
        Assert.NotNull(services.SettingsHydrationSnapshot);
    }

    [Fact]
    public void AppStartup_UsesCompositionRootInsteadOfStartupUri()
    {
        var appMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "App.xaml");
        var appSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "App.xaml.cs");

        Assert.DoesNotContain("StartupUri=", appMarkup, StringComparison.Ordinal);
        Assert.Contains("_compositionRoot = new AppCompositionRoot();", appSource, StringComparison.Ordinal);
        Assert.Contains("MainWindow = _compositionRoot.CreateMainWindow();", appSource, StringComparison.Ordinal);
    }
}
