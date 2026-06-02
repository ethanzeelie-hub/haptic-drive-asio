namespace HapticDrive.Asio.Audio.Mixing;

public sealed record AudioMixerSnapshot(
    bool IsRunning,
    bool IsMuted,
    bool EmergencyMute,
    int InputSourceCount,
    int ActiveSourceCount,
    float MasterGain,
    float PeakLevel);
