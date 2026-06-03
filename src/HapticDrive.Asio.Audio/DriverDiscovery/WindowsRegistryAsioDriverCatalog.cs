using Microsoft.Win32;
using System.Runtime.Versioning;

namespace HapticDrive.Asio.Audio.DriverDiscovery;

public sealed class WindowsRegistryAsioDriverCatalog : IAsioDriverCatalog
{
    private const string AsioRegistryPath = @"SOFTWARE\ASIO";

    public ValueTask<IReadOnlyList<string>> GetDriverNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var driverNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadDriverNames(RegistryHive.LocalMachine, RegistryView.Registry64, driverNames);
        ReadDriverNames(RegistryHive.LocalMachine, RegistryView.Registry32, driverNames);
        ReadDriverNames(RegistryHive.CurrentUser, RegistryView.Registry64, driverNames);
        ReadDriverNames(RegistryHive.CurrentUser, RegistryView.Registry32, driverNames);

        return ValueTask.FromResult<IReadOnlyList<string>>(driverNames.ToArray());
    }

    [SupportedOSPlatform("windows")]
    private static void ReadDriverNames(
        RegistryHive hive,
        RegistryView view,
        ISet<string> driverNames)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var asioKey = baseKey.OpenSubKey(AsioRegistryPath);
            if (asioKey is null)
            {
                return;
            }

            foreach (var subKeyName in asioKey.GetSubKeyNames())
            {
                if (!string.IsNullOrWhiteSpace(subKeyName))
                {
                    driverNames.Add(subKeyName.Trim());
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // ASIO discovery is diagnostic only; inaccessible registry hives must not break startup or tests.
        }
    }
}
