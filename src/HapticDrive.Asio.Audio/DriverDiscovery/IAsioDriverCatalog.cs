namespace HapticDrive.Asio.Audio.DriverDiscovery;

public interface IAsioDriverCatalog
{
    ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default);
}
