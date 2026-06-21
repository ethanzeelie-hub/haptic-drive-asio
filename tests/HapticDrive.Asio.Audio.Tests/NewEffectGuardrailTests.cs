using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class NewEffectGuardrailTests
{
    [Fact]
    public void DiagnosticEffectRequiresNoMainWindowSwitch()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "HapticDrive.Asio.App",
            "MainWindow.xaml.cs")));
        var defaults = HapticEffectSettingsTranslator.CreateDefaultDocuments(BuiltInHapticEffectRegistry.Instance);

        Assert.Contains("diagnostic-test", defaults.Keys);
        Assert.DoesNotContain("diagnostic-test", source, StringComparison.OrdinalIgnoreCase);
    }
}
