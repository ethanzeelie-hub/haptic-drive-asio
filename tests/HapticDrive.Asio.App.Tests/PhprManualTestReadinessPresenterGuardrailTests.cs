using System.IO;

namespace HapticDrive.Asio.App.Tests;

public sealed class PhprManualTestReadinessPresenterGuardrailTests
{
    [Fact]
    public void PresenterSource_HasNoWpfControlOrHardwareWriteReferences()
    {
        var source = File.ReadAllText(Path.Combine(
            MainWindowSourceTestHelper.FindRepositoryRoot(),
            "src",
            "HapticDrive.Asio.App",
            "PhprManualTestReadinessPresenter.cs"));

        Assert.DoesNotContain("System.Windows", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsHidReportWriter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SimagicPhprOutputDevice", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".SendAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckBox", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowSource_UsesPhprManualTestReadinessPresenter()
    {
        var source = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();

        Assert.Contains("PhprManualTestReadinessPresenter.Build(new PhprManualTestReadinessSnapshot(", source, StringComparison.Ordinal);
        Assert.Contains("PhprPedalsChecklistItemsControl.ItemsSource = readinessPresentation.ChecklistItems;", source, StringComparison.Ordinal);
    }
}
