using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public void EffectSettingsAreGeneratedFromRegistry()
    {
        var effectsViewMarkup = MainWindowSourceTestHelper.ReadRepositoryFile(
            "src",
            "HapticDrive.Asio.App",
            "Views",
            "EffectsView.xaml");
        var mainWindowSource = MainWindowSourceTestHelper.ReadCombinedMainWindowSource();
        var registry = BuiltInHapticEffectRegistry.Instance;
        var items = EffectSettingsListViewModel.BuildItems(HapticDriveProfile.Default, registry, _ => { });

        Assert.Contains("GeneratedEffectSettingsItemsControl", effectsViewMarkup, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Items}\"", effectsViewMarkup, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ResetToDefaultsCommand}\"", effectsViewMarkup, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Unit, StringFormat=Unit: {0}}\"", effectsViewMarkup, StringComparison.Ordinal);
        Assert.Contains("EffectsViewControl.DataContext = _effectSettingsViewModel;", mainWindowSource, StringComparison.Ordinal);
        Assert.Equal(registry.All.Count, items.Count);
    }
}
