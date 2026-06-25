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

    [Fact]
    public void OutputInterlock_RemainsSingleSharedInstanceAcrossAppAndPipelineCreation()
    {
        var compositionRootSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppCompositionRoot.cs");
        var runtimeSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppRuntimeSession.cs");
        var pipelineSource = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppRuntimeSession.PhprAndPipeline.cs");

        Assert.Contains("var outputInterlock = new OutputInterlock();", compositionRootSource, StringComparison.Ordinal);
        Assert.Contains("outputInterlock,", compositionRootSource, StringComparison.Ordinal);
        Assert.Contains("_outputInterlock = services.OutputInterlock;", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("outputInterlock: _outputInterlock,", pipelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new OutputInterlock()", pipelineSource, StringComparison.Ordinal);
    }
}
