namespace HapticDrive.Asio.Audio.Mixing;

public readonly record struct AudioMixerSnapshot(
    bool IsRunning,
    bool IsMuted,
    bool EmergencyMute,
    int InputSourceCount,
    int ActiveSourceCount,
    float MasterGain,
    float PeakLevel);
