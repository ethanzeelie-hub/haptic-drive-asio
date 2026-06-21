namespace HapticDrive.Asio.Core.Telemetry;

public sealed record UdpTelemetryReceiverSnapshot(
    bool IsRunning,
    int ConfiguredPort,
    int BoundPort,
    long PacketCount,
    long IgnoredRemotePacketCount,
    long OversizedDatagramCount,
    double PacketRatePerSecond,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastPacketAtUtc,
    TimeSpan? TimeSinceLastPacket,
    bool HasNoPacketWarning,
    long ErrorCount,
    string? LastErrorMessage);
