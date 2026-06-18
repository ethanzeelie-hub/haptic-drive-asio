using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Core.Telemetry;

public interface IGameTelemetryAdapter
{
    string GameName { get; }

    VehicleState CurrentVehicleState { get; }

    IReadOnlyList<TelemetryPacketDescriptor> PacketDescriptors { get; }

    TelemetryPacketProcessResult Process(UdpTelemetryPacket packet);
}

public sealed record TelemetryPacketDescriptor(
    byte PacketId,
    string Name);

public enum TelemetryPacketParseStatus
{
    Success,
    Ignored,
    Failure
}

public enum TelemetryVehicleStateUpdateStatus
{
    Applied,
    Ignored
}

public sealed record TelemetryVehicleStateUpdateResult(
    TelemetryVehicleStateUpdateStatus Status,
    VehicleState State,
    string Message)
{
    public bool WasApplied => Status == TelemetryVehicleStateUpdateStatus.Applied;

    public bool WasIgnored => Status == TelemetryVehicleStateUpdateStatus.Ignored;

    public static TelemetryVehicleStateUpdateResult Applied(VehicleState state, string message)
    {
        return new(TelemetryVehicleStateUpdateStatus.Applied, state, message);
    }

    public static TelemetryVehicleStateUpdateResult Ignored(VehicleState state, string message)
    {
        return new(TelemetryVehicleStateUpdateStatus.Ignored, state, message);
    }
}

public sealed record TelemetryPacketProcessResult(
    TelemetryPacketParseStatus ParseStatus,
    byte? PacketId,
    string PacketMessage,
    TelemetryVehicleStateUpdateResult VehicleStateUpdate);
