using HapticDrive.Asio.Core.Telemetry;

namespace HapticDrive.Asio.Core.Games;

public sealed record GameIntegrationDescriptor(
    GameIntegrationId Id,
    string DisplayName,
    string TelemetryProtocolName,
    string TelemetryProtocolVersion,
    GameTelemetryEndpointDefaults EndpointDefaults,
    GameCapabilities Capabilities,
    IReadOnlyList<TelemetryPacketDescriptor> PacketDescriptors,
    Func<IGameTelemetryAdapter> CreateAdapter);
