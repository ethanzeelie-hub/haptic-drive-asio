namespace HapticDrive.Asio.Core.Games;

public readonly record struct TelemetryPacketKind(byte Id, string Name)
{
    public override string ToString() => Name;
}
