namespace HapticDrive.Asio.Audio.Effects.Registry;

public sealed record EffectParameterDescriptor(
    string Key,
    string DisplayName,
    EffectParameterKind Kind,
    double Minimum,
    double Maximum,
    double DefaultValue,
    double Step,
    int DecimalPlaces,
    string Unit,
    bool IsAdvanced);
