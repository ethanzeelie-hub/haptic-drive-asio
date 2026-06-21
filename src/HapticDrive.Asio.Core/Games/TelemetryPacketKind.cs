namespace HapticDrive.Asio.Core.Games;

public sealed record TelemetryPacketKind(string Value)
{
    public override string ToString() => Value;
}
