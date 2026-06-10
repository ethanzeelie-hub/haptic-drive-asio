namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record ManualAsioHardwareTestSnapshot(
    bool IsActive,
    string TestMode,
    string SelectedAsioDriver,
    int? SelectedOutputChannel,
    bool AsioRunning,
    bool AsioArmed,
    bool HapticsRunning,
    bool EmergencyMute,
    bool NormalMute,
    float OutputPeakLevel,
    long FramesSubmitted,
    long FramesRendered,
    long RenderCallbackCount,
    string? BlockedReason,
    string? LastTestSignal,
    TimeSpan? LastTestDuration,
    string? LastError);
