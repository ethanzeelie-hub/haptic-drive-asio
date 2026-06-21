namespace HapticDrive.Asio.Core.Games;

public interface IGameIntegrationRegistry
{
    IReadOnlyList<GameIntegrationDescriptor> All { get; }

    GameIntegrationDescriptor Default { get; }

    GameIntegrationDescriptor GetRequired(GameIntegrationId id);
}
