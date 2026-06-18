using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed class F125GameTelemetryAdapter : IGameTelemetryAdapter
{
    private static readonly IReadOnlyList<TelemetryPacketDescriptor> PacketDescriptorsValue = F125PacketDefinitions.All
        .Select(definition => new TelemetryPacketDescriptor(definition.Id, definition.Name))
        .ToArray();

    private readonly F125VehicleStateAdapter _vehicleStateAdapter = new();

    public string GameName => "F1 25";

    public VehicleState CurrentVehicleState => _vehicleStateAdapter.Current;

    public IReadOnlyList<TelemetryPacketDescriptor> PacketDescriptors => PacketDescriptorsValue;

    public TelemetryPacketProcessResult Process(UdpTelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var parseResult = F125PacketParser.Parse(packet.Payload);
        var vehicleStateUpdate = _vehicleStateAdapter.Apply(parseResult);

        return new TelemetryPacketProcessResult(
            Map(parseResult.Status),
            parseResult.Header?.PacketId,
            parseResult.Message,
            Map(vehicleStateUpdate));
    }

    private static TelemetryPacketParseStatus Map(F125PacketParseStatus status)
    {
        return status switch
        {
            F125PacketParseStatus.Success => TelemetryPacketParseStatus.Success,
            F125PacketParseStatus.Ignored => TelemetryPacketParseStatus.Ignored,
            F125PacketParseStatus.Failure => TelemetryPacketParseStatus.Failure,
            _ => TelemetryPacketParseStatus.Failure
        };
    }

    private static TelemetryVehicleStateUpdateResult Map(F125VehicleStateUpdateResult result)
    {
        return result.WasApplied
            ? TelemetryVehicleStateUpdateResult.Applied(result.State, result.Message)
            : TelemetryVehicleStateUpdateResult.Ignored(result.State, result.Message);
    }
}
