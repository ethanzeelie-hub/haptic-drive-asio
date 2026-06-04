using System.Security.Cryptography;
using System.Text;
using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Windows;

public static class WindowsInputDevicePathSanitizer
{
    public static string? Sanitize(string? devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
        {
            return null;
        }

        var trimmed = devicePath.Trim();
        var withoutPrefix = trimmed.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
            ? trimmed[4..]
            : trimmed;
        var parts = withoutPrefix.Split('#', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0]}#{parts[1]}#<redacted>";
        }

        return withoutPrefix.Length <= 96 ? withoutPrefix : $"{withoutPrefix[..96]}...";
    }

    public static string CreateStableDeviceId(InputDiscoveryMethod method, string? rawIdentity, int index)
    {
        var identity = string.IsNullOrWhiteSpace(rawIdentity)
            ? $"{method}:{index}"
            : $"{method}:{rawIdentity}:{index}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"{method.ToString().ToLowerInvariant()}:{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }
}
