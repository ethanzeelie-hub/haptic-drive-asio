using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using Microsoft.Win32;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

#pragma warning disable CA1416
public sealed partial class WindowsRegistryHidPhprDirectOutputCandidateProvider : IPHprDirectOutputCandidateProvider
{
    private const string HidRegistryPath = @"SYSTEM\CurrentControlSet\Enum\HID";

    public IReadOnlyList<PHprDirectOutputCandidate> DiscoverCandidates(DateTimeOffset? discoveredAtUtc = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(HidRegistryPath);
            if (root is null)
            {
                return [];
            }

            var candidates = new List<PHprDirectOutputCandidate>();
            foreach (var deviceKeyName in root.GetSubKeyNames())
            {
                using var deviceKey = root.OpenSubKey(deviceKeyName);
                if (deviceKey is null)
                {
                    continue;
                }

                foreach (var instanceKeyName in deviceKey.GetSubKeyNames())
                {
                    using var instanceKey = deviceKey.OpenSubKey(instanceKeyName);
                    if (instanceKey is null)
                    {
                        continue;
                    }

                    var candidate = TryBuildCandidate(deviceKeyName, instanceKeyName, instanceKey, candidates.Count);
                    if (candidate is not null)
                    {
                        candidates.Add(candidate.Score());
                    }
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.HasOutputOrFeatureReportCapability)
                .ThenByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.VendorProductText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.SafeDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static PHprDirectOutputCandidate? TryBuildCandidate(
        string deviceKeyName,
        string instanceKeyName,
        RegistryKey instanceKey,
        int index)
    {
        var hardwareIds = GetStringValues(instanceKey, "HardwareID");
        var compatibleIds = GetStringValues(instanceKey, "CompatibleIDs");
        var identityText = string.Join(" ", deviceKeyName, instanceKeyName, string.Join(" ", hardwareIds), string.Join(" ", compatibleIds));
        var vendorId = ParseHexToken(identityText, "VID");
        if (!SimagicPhprDeviceIdentity.IsSimagicFamilyVendor(vendorId))
        {
            return null;
        }

        var productId = ParseHexToken(identityText, "PID");
        var displayName = FirstNonEmpty(
            GetStringValue(instanceKey, "FriendlyName"),
            GetStringValue(instanceKey, "DeviceDesc"),
            GetStringValue(instanceKey, "DeviceDescription"),
            "HID registry metadata");
        var metadataId = CreateMetadataId(identityText, index);

        return new PHprDirectOutputCandidate
        {
            CandidateId = metadataId,
            DevicePath = metadataId,
            DisplayName = $"HID registry metadata ({BuildVendorProduct(vendorId, productId)}; {displayName})",
            DeviceClass = FirstNonEmpty(GetStringValue(instanceKey, "Class"), "HID registry metadata"),
            SourceMethod = PHprDirectOutputCandidateSourceMethod.HidRegistryMetadata,
            VendorId = vendorId,
            ProductId = productId,
            InterfaceNumber = ParseTextToken(identityText, InterfaceRegex()),
            CollectionNumber = ParseTextToken(identityText, CollectionRegex())
        };
    }

    private static string CreateMetadataId(string identityText, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{identityText}:{index.ToString(CultureInfo.InvariantCulture)}"));
        return $"registry-hid:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private static string BuildVendorProduct(ushort? vendorId, ushort? productId)
    {
        return vendorId is null || productId is null
            ? "VID/PID unavailable"
            : $"VID_{vendorId:X4}/PID_{productId:X4}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "Unknown HID device";
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
