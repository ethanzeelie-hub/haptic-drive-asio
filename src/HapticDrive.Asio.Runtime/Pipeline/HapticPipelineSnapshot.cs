using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;
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
    NullAudioOutputDeviceSnapshot? NullOutput,
    UdpTelemetryForwarderSnapshot Forwarding,
    IReadOnlyList<HapticPipelinePacketDiagnostics> PacketDiagnostics,
    TelemetryRecordingSnapshot Recording,
    TelemetryReplaySnapshot Replay)
{
    public bool HasParsedPackets => ParserSuccessCount > 0 || ParserIgnoredCount > 0 || ParserFailureCount > 0;
}

public sealed record HapticPipelinePacketDiagnostics(
    byte PacketId,
    string Name,
    long ObservedCount,
    DateTimeOffset? LastObservedAtUtc);
