namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed record EffectSettingsDocument(
    string EffectKey,
    bool Enabled,
    IReadOnlyDictionary<string, double> Parameters);
