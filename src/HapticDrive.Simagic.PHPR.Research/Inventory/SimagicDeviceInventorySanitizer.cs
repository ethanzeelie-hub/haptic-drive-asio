using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public static partial class SimagicDeviceInventorySanitizer
{
    public static string? SanitizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = UserProfileRegex().Replace(value.Trim(), "$1<redacted>");
        sanitized = sanitized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
            ? sanitized[4..]
            : sanitized;

        if (sanitized.Contains('#', StringComparison.Ordinal))
        {
            return SanitizeHashSeparatedIdentifier(sanitized);
        }

        if (sanitized.Contains('\\', StringComparison.Ordinal))
        {
            return SanitizeSlashSeparatedIdentifier(sanitized, '\\');
        }

        if (sanitized.Contains('/', StringComparison.Ordinal))
        {
            return SanitizeSlashSeparatedIdentifier(sanitized, '/');
        }

        return LooksSensitiveSegment(sanitized) ? "<redacted>" : sanitized;
    }

    public static SimagicDeviceInventoryItem SanitizeItem(SimagicDeviceInventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item with
        {
            SafeInstanceId = SanitizeIdentifier(item.SafeInstanceId),
            SafeDevicePath = SanitizeIdentifier(item.SafeDevicePath),
            EndpointSummaries = item.EndpointSummaries
                .Select(summary => SanitizeIdentifier(summary) ?? "")
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .ToArray()
        };
    }

    public static SimagicDeviceInventorySnapshot SanitizeSnapshot(SimagicDeviceInventorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot with
        {
            Items = snapshot.Items.Select(SanitizeItem).ToArray()
        };
    }

    public static string CreateStableDeviceId(SimagicDeviceInventoryMethod method, string? rawIdentity, int index)
    {
        var identity = string.IsNullOrWhiteSpace(rawIdentity)
            ? $"{method}:{index}"
            : $"{method}:{rawIdentity}:{index}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"{method.ToString().ToLowerInvariant()}:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private static string SanitizeHashSeparatedIdentifier(string value)
    {
        var parts = value.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "<redacted>";
        }

        var sanitized = new List<string>();
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (index <= 1 || ContainsHardwareToken(part) || !LooksSensitiveSegment(part))
            {
                sanitized.Add(part);
            }
            else
            {
                AddRedactionMarker(sanitized);
            }
        }

        return string.Join("#", sanitized);
    }

    private static string SanitizeSlashSeparatedIdentifier(string value, char separator)
    {
        var parts = value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "<redacted>";
        }

        var sanitized = new List<string>();
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (index == 0 || ContainsHardwareToken(part) || !LooksSensitiveSegment(part))
            {
                sanitized.Add(part);
            }
            else
            {
                AddRedactionMarker(sanitized);
            }
        }

        return string.Join(separator, sanitized);
    }

    private static void AddRedactionMarker(List<string> parts)
    {
        if (parts.Count == 0 || !string.Equals(parts[^1], "<redacted>", StringComparison.Ordinal))
        {
            parts.Add("<redacted>");
        }
    }

    private static bool ContainsHardwareToken(string value)
    {
        return HardwareTokenRegex().IsMatch(value);
    }

    private static bool LooksSensitiveSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "<redacted>", StringComparison.Ordinal))
        {
            return false;
        }

        if (ContainsHardwareToken(value))
        {
            return false;
        }

        var hasDigit = value.Any(char.IsDigit);
        var hasLetters = value.Any(char.IsLetter);
        return value.Length >= 8 && hasDigit
            || value.Contains('&', StringComparison.Ordinal)
            || value.Contains('{', StringComparison.Ordinal)
            || value.Contains('}', StringComparison.Ordinal)
            || value.Contains("serial", StringComparison.OrdinalIgnoreCase)
            || (value.Length >= 16 && hasLetters);
    }

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)[^\\]+")]
    private static partial Regex UserProfileRegex();

    [GeneratedRegex(@"(?i)\b(VID_[0-9A-F]{4}|PID_[0-9A-F]{4}|MI_[0-9A-F]{2}|COL[0-9A-F]{2})\b")]
    private static partial Regex HardwareTokenRegex();
}
