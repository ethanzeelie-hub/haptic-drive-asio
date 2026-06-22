using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
using HapticDrive.Asio.Core.Vehicle.Freshness;
using HapticDrive.Asio.Recording;

namespace HapticDrive.Asio.Runtime.Pipeline;

public sealed record HapticPipelineSnapshot(
    bool IsRunning,
    HapticPipelineInputSource InputSource,
    DateTimeOffset? LastPacketAtUtc,
    DateTimeOffset? LastVehicleStateUpdateAtUtc,
    long PacketsObserved,
    long ParserSuccessCount,
    long ParserIgnoredCount,
    long ParserFailureCount,
    long VehicleStateUpdateCount,
    long RenderedBufferCount,
    TimeSpan? TelemetryAge,
    TimeSpan TelemetryMuteTimeout,
    bool TelemetryTimedOutMuted,
    bool IsMuted,
    bool EmergencyMute,
    string LastPacketMessage,
    string LastVehicleStateMessage,
    string? LastPipelineError,
    VehicleState VehicleState,
    HapticEffectEngineSnapshot Effects,
    AudioRenderPipelineSnapshot? Audio,
    AudioOutputStatus Output,
    ManualAsioHardwareTestSnapshot ManualAsioHardwareTest,
    NullAudioOutputDeviceSnapshot? NullOutput,
    UdpTelemetryForwarderSnapshot Forwarding,
    IReadOnlyList<HapticPipelinePacketDiagnostics> PacketDiagnostics,
    TelemetryRecordingSnapshot Recording,
    TelemetryReplaySnapshot Replay)
{
    public bool HasParsedPackets => ParserSuccessCount > 0 || ParserIgnoredCount > 0 || ParserFailureCount > 0;

    public OutputInterlockSnapshot OutputInterlock { get; init; } = OutputInterlockSnapshot.StartupSafeDefault();

    public VehicleSignalFreshness TelemetryFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness MotionFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness SessionFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness LapFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness ParticipantFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness CarStatusFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness DamageFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness MotionExFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public VehicleSignalFreshness EventFreshness { get; init; } = new(false, false, false, false, false, null, null);

    public HapticFrame? HapticFrame { get; init; }

    public long RenderOverrunCount { get; init; }

    public long StaleFrameSilenceCount { get; init; }

    public long InterlockSilenceCount { get; init; }

    public long MaxRenderDurationTicks { get; init; }
}

public sealed record HapticPipelinePacketDiagnostics(
    byte PacketId,
    string Name,
    long ObservedCount,
    DateTimeOffset? LastObservedAtUtc);
