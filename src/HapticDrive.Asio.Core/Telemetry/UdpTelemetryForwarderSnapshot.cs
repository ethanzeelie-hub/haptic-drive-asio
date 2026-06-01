namespace HapticDrive.Asio.Core.Telemetry;

public sealed record UdpTelemetryForwarderSnapshot(
    bool IsEnabled,
    int DestinationCount,
    int EnabledDestinationCount,
    long InputPacketCount,
    long ForwardedDatagramCount,
    long ForwardedByteCount,
    long ErrorCount,
    DateTimeOffset? LastForwardedAtUtc,
    string? LastErrorMessage);
