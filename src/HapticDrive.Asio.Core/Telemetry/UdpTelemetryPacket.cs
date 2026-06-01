using System.Net;

namespace HapticDrive.Asio.Core.Telemetry;

public sealed record UdpTelemetryPacket(
    long SequenceNumber,
    byte[] Payload,
    IPEndPoint RemoteEndPoint,
    DateTimeOffset ReceivedAtUtc);
