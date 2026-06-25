using System.IO;
using System.Windows.Input;
using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;

namespace HapticDrive.Asio.App.Tests;

public sealed class ArchitectureGuardrailTests
{
    [Fact]
    public void MainWindow_IsShellOnlyAndDelegatesRuntimeOwnership()
    {
        var source = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs");

        Assert.Contains("private readonly AppRuntimeSession _runtime;", source, StringComparison.Ordinal);
        Assert.Contains("_runtime = new AppRuntimeSession(this, services);", source, StringComparison.Ordinal);
        Assert.Contains("_runtime.InitializeAfterHostAssignment();", source, StringComparison.Ordinal);
        Assert.Contains("private void NavigationList_SelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("if (_runtime is null)", source, StringComparison.Ordinal);
        Assert.Contains("_runtime.NavigationList_SelectionChanged(sender, e);", source, StringComparison.Ordinal);
        Assert.Contains("private void MainWindow_PreviewKeyDown", source, StringComparison.Ordinal);
        Assert.Contains("=> _runtime.MainWindow_PreviewKeyDown(sender, e);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationSafetyController", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AudioOutputController", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordingReplayController", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhprOutputController", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticsPresentationController", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Controllers_RunWithoutConstructingMainWindowAndAvoidViewControlAccess()
    {
        var root = new AppCompositionRoot();
        var services = root.Services;
        var removedControlLookup = "Get" + "Required" + "Control";
        var controllerSourceDirectory = Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "Controllers");

        Assert.IsType<ApplicationSafetyController>(services.ApplicationSafetyController);
        Assert.IsType<TelemetrySessionController>(services.TelemetrySessionController);
        Assert.IsType<AudioOutputController>(services.AudioOutputController);
        Assert.IsType<RecordingReplayController>(services.RecordingReplayController);
        Assert.IsType<PhprOutputController>(services.PhprOutputController);
        Assert.IsType<DiagnosticsPresentationController>(services.DiagnosticsPresentationController);

        foreach (var path in Directory.GetFiles(controllerSourceDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var source = File.ReadAllText(path);

            Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ComboBox", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FindName(", source, StringComparison.Ordinal);
            Assert.DoesNotContain(removedControlLookup, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ViewModels_ExposeCommandsForBoundUiActions()
    {
        var effectsViewMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "Views",
            "EffectsView.xaml");

        Assert.Contains("Command=\"{Binding ResetToDefaultsCommand}\"", effectsViewMarkup, StringComparison.Ordinal);
        Assert.Equal(typeof(ICommand), typeof(EffectSettingsItemViewModel).GetProperty(nameof(EffectSettingsItemViewModel.ResetToDefaultsCommand))!.PropertyType);
    }

    [Fact]
    public void TestSources_DoNotInstantiateRealPhprHidWriters()
    {
        var testsDirectory = Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "tests");
        var windowsWriter = "new Windows" + "HidReportWriter";
        var deferredWriter = "new DeferredWindows" + "HidReportWriter";

        foreach (var path in Directory.GetFiles(testsDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);

            Assert.DoesNotContain(windowsWriter, source, StringComparison.Ordinal);
            Assert.DoesNotContain(deferredWriter, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ManualBst1PulsePath_RemainsIndependentFromLiveTelemetryAndDrivingState()
    {
        var source = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "AppRuntimeSession.ProfileTheme.cs");
        var methodStart = source.IndexOf("private async void ManualBst1PulseButton_Click", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void ManualAsioHardwareTestChannel0Button_Click", StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);

        var methodSource = source[methodStart..methodEnd];

        Assert.Contains("await StartManualAsioHardwareTestAsync(new ManualAsioHardwareTestRequest(", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DrivingArmed", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VehicleState", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TelemetryReceiver", methodSource, StringComparison.Ordinal);
    }
}
