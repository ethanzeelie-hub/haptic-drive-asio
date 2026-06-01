namespace HapticDrive.Asio.Telemetry.F1_25;

public sealed record F125PacketDefinition(
    byte Id,
    F125PacketKind Kind,
    string Name,
    int Size,
    byte Version,
    bool IsV1RequiredPacket);
