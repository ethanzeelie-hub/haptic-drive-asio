namespace HapticDrive.Asio.Audio.DriverDiscovery;

public sealed class UnavailableAsioDriverCatalog : IAsioDriverCatalog
{
    public ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
