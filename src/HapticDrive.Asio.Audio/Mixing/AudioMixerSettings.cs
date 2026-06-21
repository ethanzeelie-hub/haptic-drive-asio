namespace HapticDrive.Asio.Audio.Mixing;

public readonly record struct AudioMixerSettings(
    float MasterGain,
    bool IsMuted,
    bool EmergencyMute)
{
    public static AudioMixerSettings Default { get; } = new(
        MasterGain: 1f,
        IsMuted: false,
        EmergencyMute: false);
}
