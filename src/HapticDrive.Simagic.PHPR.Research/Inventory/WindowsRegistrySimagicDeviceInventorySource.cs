using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HapticDrive.Simagic.PHPR.Research.Inventory;

#pragma warning disable CA1416
public sealed partial class WindowsRegistrySimagicDeviceInventorySource(
    string registryPath,
    SimagicDeviceInventoryMethod method,
    string displayName) : ISimagicDeviceInventorySource
{
    public SimagicDeviceInventoryMethod Method { get; } = method;

    public ValueTask<SimagicDeviceInventorySourceResult> EnumerateAsync(
        DateTimeOffset discoveredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.FromResult(SimagicDeviceInventorySourceResult.Empty);
        }

        var items = new List<SimagicDeviceInventoryItem>();
        var errors = new List<SimagicDeviceInventoryError>();

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(registryPath);
            if (root is null)
            {
                return ValueTask.FromResult(SimagicDeviceInventorySourceResult.Empty);
            }

            foreach (var deviceKeyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var deviceKey = root.OpenSubKey(deviceKeyName);
                if (deviceKey is null)
                {
                    continue;
                }

                foreach (var instanceKeyName in deviceKey.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var instanceKey = deviceKey.OpenSubKey(instanceKeyName);
                        if (instanceKey is null)
                        {
                            continue;
                        }

                        items.Add(BuildItem(deviceKeyName, instanceKeyName, instanceKey, discoveredAtUtc));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add(new SimagicDeviceInventoryError(Method, $"{displayName} {deviceKeyName}: {ex.Message}"));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add(new SimagicDeviceInventoryError(Method, $"{displayName}: {ex.Message}"));
        }

        return ValueTask.FromResult(new SimagicDeviceInventorySourceResult(items, errors));
    }

    private SimagicDeviceInventoryItem BuildItem(
        string deviceKeyName,
        string instanceKeyName,
        RegistryKey instanceKey,
        DateTimeOffset discoveredAtUtc)
    {
        var rawInstanceId = $"{GetRootLabel()}\\{deviceKeyName}\\{instanceKeyName}";
        var hardwareIds = GetStringValues(instanceKey, "HardwareID");
        var compatibleIds = GetStringValues(instanceKey, "CompatibleIDs");
        var identityText = string.Join(" ", rawInstanceId, string.Join(" ", hardwareIds), string.Join(" ", compatibleIds));
        var driverKeyName = GetStringValue(instanceKey, "Driver");
        var driver = TryReadDriverMetadata(driverKeyName);
        var display = FirstNonEmpty(
            GetStringValue(instanceKey, "FriendlyName"),
            GetStringValue(instanceKey, "DeviceDesc"),
            GetStringValue(instanceKey, "DeviceDescription"),
            deviceKeyName,
            displayName);
        var manufacturer = GetStringValue(instanceKey, "Mfg");

        return new SimagicDeviceInventoryItem
        {
            DeviceId = SimagicDeviceInventorySanitizer.CreateStableDeviceId(Method, rawInstanceId, 0),
            DisplayName = display,
            Manufacturer = manufacturer,
            ProductName = FirstNonEmpty(GetStringValue(instanceKey, "FriendlyName"), GetStringValue(instanceKey, "DeviceDesc"), display),
            ServiceName = GetStringValue(instanceKey, "Service"),
            DriverProvider = driver.Provider,
            DriverVersion = driver.Version,
            DeviceClass = FirstNonEmpty(GetStringValue(instanceKey, "Class"), displayName),
            ClassGuid = GetStringValue(instanceKey, "ClassGUID"),
            VendorId = ParseHexToken(identityText, "VID"),
            ProductId = ParseHexToken(identityText, "PID"),
            InterfaceNumber = ParseTextToken(identityText, InterfaceRegex()),
            CollectionNumber = ParseTextToken(identityText, CollectionRegex()),
            SafeInstanceId = SimagicDeviceInventorySanitizer.SanitizeIdentifier(rawInstanceId),
            SafeDevicePath = SimagicDeviceInventorySanitizer.SanitizeIdentifier(rawInstanceId),
            DiscoveryMethod = Method,
            ReadOnlyDiscoverySucceeded = true,
            DiscoveredAtUtc = discoveredAtUtc
        };
    }

    private string GetRootLabel()
    {
        return Method == SimagicDeviceInventoryMethod.WindowsRegistryHid ? "HID" : "USB";
    }

    private static (string? Provider, string? Version) TryReadDriverMetadata(string? driverKeyName)
    {
        if (string.IsNullOrWhiteSpace(driverKeyName) || !OperatingSystem.IsWindows())
        {
            return (null, null);
        }

        try
        {
            using var driverKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Class\{driverKeyName}");
            if (driverKey is null)
            {
                return (null, null);
            }

            return (
                FirstNonEmpty(GetStringValue(driverKey, "ProviderName"), GetStringValue(driverKey, "DriverProvider")),
                GetStringValue(driverKey, "DriverVersion"));
        }
        catch
        {
            return (null, null);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "Unknown device";
    }

    private static string? GetStringValue(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            string text => CleanRegistryString(text),
            string[] values => values.Select(CleanRegistryString).FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)),
            _ => null
        };
    }

    private static IReadOnlyList<string> GetStringValues(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            string text => [CleanRegistryString(text)],
            string[] values => values.Select(CleanRegistryString).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray(),
            _ => []
        };
    }

    private static string CleanRegistryString(string value)
    {
        var trimmed = value.Trim();
        var lastSemicolon = trimmed.LastIndexOf(';');
        return trimmed.StartsWith("@", StringComparison.Ordinal) && lastSemicolon >= 0 && lastSemicolon + 1 < trimmed.Length
            ? trimmed[(lastSemicolon + 1)..].Trim()
            : trimmed;
    }

    private static ushort? ParseHexToken(string value, string token)
    {
        var match = Regex.Match(value, $@"(?i)\b{token}_([0-9A-F]{{4}})\b");
        if (!match.Success)
        {
            return null;
        }

        return ushort.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ParseTextToken(string value, Regex regex)
    {
        var match = regex.Match(value);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"(?i)\bMI_([0-9A-F]{2})\b")]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"(?i)\bCOL([0-9A-F]{2})\b")]
    private static partial Regex CollectionRegex();
}
#pragma warning restore CA1416
