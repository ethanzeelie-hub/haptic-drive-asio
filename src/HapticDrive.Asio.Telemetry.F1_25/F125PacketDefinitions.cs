namespace HapticDrive.Asio.Telemetry.F1_25;

public static class F125PacketDefinitions
{
    public const int HeaderSize = 29;
    public const ushort PacketFormat = 2025;
    public const byte GameYear = 25;
    public const byte PacketVersion = 1;

    public static IReadOnlyList<F125PacketDefinition> All { get; } =
    [
        new(0, F125PacketKind.Motion, "Motion", 1349, PacketVersion, true),
        new(1, F125PacketKind.Session, "Session", 753, PacketVersion, true),
        new(2, F125PacketKind.LapData, "Lap Data", 1285, PacketVersion, true),
        new(3, F125PacketKind.Event, "Event", 45, PacketVersion, true),
        new(4, F125PacketKind.Participants, "Participants", 1284, PacketVersion, true),
        new(5, F125PacketKind.CarSetups, "Car Setups", 1133, PacketVersion, false),
        new(6, F125PacketKind.CarTelemetry, "Car Telemetry", 1352, PacketVersion, true),
        new(7, F125PacketKind.CarStatus, "Car Status", 1239, PacketVersion, true),
        new(8, F125PacketKind.FinalClassification, "Final Classification", 1042, PacketVersion, false),
        new(9, F125PacketKind.LobbyInfo, "Lobby Info", 954, PacketVersion, false),
        new(10, F125PacketKind.CarDamage, "Car Damage", 1041, PacketVersion, true),
        new(11, F125PacketKind.SessionHistory, "Session History", 1460, PacketVersion, false),
        new(12, F125PacketKind.TyreSets, "Tyre Sets", 231, PacketVersion, false),
        new(13, F125PacketKind.MotionEx, "Motion Ex", 273, PacketVersion, true),
        new(14, F125PacketKind.TimeTrial, "Time Trial", 101, PacketVersion, false),
        new(15, F125PacketKind.LapPositions, "Lap Positions", 1131, PacketVersion, false)
    ];

    private static readonly IReadOnlyDictionary<byte, F125PacketDefinition> ById = All.ToDictionary(packet => packet.Id);

    public static bool TryGetById(byte packetId, out F125PacketDefinition? definition)
    {
        return ById.TryGetValue(packetId, out definition);
    }
}
