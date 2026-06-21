namespace HapticDrive.Asio.Audio.Safety;

public readonly record struct AudioSafetyProcessorOptions(
    float OutputGain,
    float OutputGainCeiling,
    bool LimiterEnabled,
    bool EmergencyMute)
{
    public const float DefaultOutputGain = 1f;
    public const float DefaultOutputGainCeiling = 1f;

    public static AudioSafetyProcessorOptions Default { get; } = new(
        OutputGain: DefaultOutputGain,
        OutputGainCeiling: DefaultOutputGainCeiling,
        LimiterEnabled: true,
        EmergencyMute: false);
}
