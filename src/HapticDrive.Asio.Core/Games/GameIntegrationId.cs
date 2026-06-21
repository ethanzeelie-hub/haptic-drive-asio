namespace HapticDrive.Asio.Core.Games;

public sealed record GameIntegrationId(string Value)
{
    public override string ToString() => Value;
}
