namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed record EffectParameterDescriptor(
    string Key,
    string DisplayName,
    double Minimum,
    double Maximum,
    double DefaultValue,
    double Step,
    string Unit,
    bool IsAdvanced);
