namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed record F125Packet(
    F125PacketHeader Header,
    F125PacketDefinition Definition,
    F125PacketBody Body);
