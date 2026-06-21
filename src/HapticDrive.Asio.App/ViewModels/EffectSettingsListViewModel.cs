using System.Collections.ObjectModel;
using System.Windows.Input;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App.ViewModels;

internal sealed class EffectSettingsListViewModel : ObservableObject
{
    public ObservableCollection<EffectSettingsItemViewModel> Items { get; } = [];

    public void ReplaceWith(
        IEnumerable<EffectSettingsItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    public static IReadOnlyList<EffectSettingsItemViewModel> BuildItems(
        HapticDriveProfile profile,
        IHapticEffectRegistry registry,
        Action<string> resetToDefaults)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resetToDefaults);

        return registry.All
            .Select(descriptor =>
            {
                profile.EffectSettings.TryGetValue(descriptor.Key, out var document);
                document ??= descriptor.CreateDefaultSettings();
                var validationMessages = descriptor.Validate(document);
                var parameters = descriptor.Parameters
                    .Select(parameter => new EffectParameterItemViewModel(
                        parameter.DisplayName,
                        parameter.Key,
                        parameter.Unit,
                        parameter.Minimum,
                        parameter.Maximum,
                        parameter.DefaultValue,
                        document.Parameters.TryGetValue(parameter.Key, out var value)
                            ? value
                            : parameter.DefaultValue))
                    .ToArray();

                return new EffectSettingsItemViewModel(
                    descriptor.DisplayName,
                    descriptor.Key,
                    descriptor.Category.ToString(),
                    document.Enabled,
                    string.Join(", ", descriptor.RequiredSignals.Select(signal => signal.SignalName)),
                    validationMessages.Count == 0
                        ? "Valid"
                        : string.Join(Environment.NewLine, validationMessages),
                    parameters,
                    new RelayCommand(() => resetToDefaults(descriptor.Key)));
            })
            .ToArray();
    }
}

internal sealed record EffectSettingsItemViewModel(
    string DisplayName,
    string EffectKey,
    string Category,
    bool Enabled,
    string RequiredSignalsText,
    string ValidationText,
    IReadOnlyList<EffectParameterItemViewModel> Parameters,
    ICommand ResetToDefaultsCommand);

internal sealed record EffectParameterItemViewModel(
    string DisplayName,
    string Key,
    string Unit,
    double Minimum,
    double Maximum,
    double DefaultValue,
    double CurrentValue);
