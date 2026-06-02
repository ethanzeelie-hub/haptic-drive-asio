namespace HapticDrive.Asio.Audio.Mixing;

public sealed record AudioMixerSettings(
    float MasterGain,
    bool IsMuted,
    bool EmergencyMute)
{
    public static AudioMixerSettings Default { get; } = new(
        MasterGain: 1f,
        IsMuted: false,
        EmergencyMute: false);
}
