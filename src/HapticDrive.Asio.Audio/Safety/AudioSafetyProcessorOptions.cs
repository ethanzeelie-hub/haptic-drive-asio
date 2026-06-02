namespace HapticDrive.Asio.Audio.Safety;

public sealed record AudioSafetyProcessorOptions(
    float OutputGain,
    float OutputGainCeiling,
    bool LimiterEnabled,
    bool EmergencyMute)
{
    public const float DefaultOutputGain = 0.25f;
    public const float DefaultOutputGainCeiling = 0.75f;

    public static AudioSafetyProcessorOptions Default { get; } = new(
        OutputGain: DefaultOutputGain,
        OutputGainCeiling: DefaultOutputGainCeiling,
        LimiterEnabled: true,
        EmergencyMute: false);
}
