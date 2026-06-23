using HapticDrive.Asio.Audio.Effects.Registry;

namespace HapticDrive.Asio.Audio.Effects;

internal sealed class HapticEffectGraphSnapshot
{
    public HapticEffectGraphSnapshot(
        IReadOnlyDictionary<string, EffectSettingsDocument> effectSettings,
        HapticEffectEngineOptions options)
    {
        EffectSettings = effectSettings ?? throw new ArgumentNullException(nameof(effectSettings));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IReadOnlyDictionary<string, EffectSettingsDocument> EffectSettings { get; }

    public HapticEffectEngineOptions Options { get; }
}
