namespace HapticDrive.Asio.Audio.Effects.Registry;

public interface IHapticEffectDescriptor
{
    string Key { get; }

    string DisplayName { get; }

    string Description { get; }

    HapticEffectCategory Category { get; }

    IReadOnlyList<HapticSignalRequirement> RequiredSignals { get; }

    IReadOnlyList<EffectParameterDescriptor> Parameters { get; }

    EffectSettingsDocument CreateDefaultSettings();

    EffectSettingsDocument Normalize(
        EffectSettingsDocument? settings,
        ICollection<string>? messages = null);

    IHapticEffectRuntime CreateRuntime(EffectSettingsDocument settings);

    IReadOnlyList<string> Validate(EffectSettingsDocument settings);
}
