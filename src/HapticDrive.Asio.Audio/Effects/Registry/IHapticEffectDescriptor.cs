namespace HapticDrive.Asio.Audio.Effects.Registry;

public interface IHapticEffectDescriptor
{
    string Key { get; }

    string DisplayName { get; }

    HapticEffectCategory Category { get; }

    IReadOnlyList<HapticSignalRequirement> RequiredSignals { get; }

    IReadOnlyList<EffectParameterDescriptor> Parameters { get; }

    EffectSettingsDocument CreateDefaultSettings();

    IHapticEffectRuntime CreateRuntime(EffectSettingsDocument settings);

    IReadOnlyList<string> Validate(EffectSettingsDocument settings);
}
