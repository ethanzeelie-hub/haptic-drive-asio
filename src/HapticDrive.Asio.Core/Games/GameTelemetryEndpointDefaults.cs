using System.Net;

namespace HapticDrive.Asio.Core.Games;

public sealed record GameTelemetryEndpointDefaults(
    int UdpPort,
    IPAddress BindAddress,
    bool AllowLanTelemetry);
