using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

internal static class SimagicPayloadHex
{
    public static bool TryParse(ReadOnlySpan<char> value, out byte[] bytes)
    {
        var compact = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (Uri.IsHexDigit(character))
            {
                compact.Append(character);
            }
            else if (!char.IsWhiteSpace(character) && character != ':' && character != '-' && character != ',')
            {
                bytes = [];
                return false;
            }
        }

        if (compact.Length == 0 || compact.Length % 2 != 0)
        {
            bytes = [];
            return false;
        }

        bytes = new byte[compact.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            if (!byte.TryParse(
                    compact.ToString(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out bytes[index]))
            {
                bytes = [];
                return false;
            }
        }

        return true;
    }

    public static string Fingerprint(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    public static string Preview(ReadOnlySpan<byte> bytes, int maxBytes = 8)
    {
        if (bytes.Length == 0)
        {
            return "";
        }

        var visibleBytes = Math.Min(bytes.Length, Math.Max(1, maxBytes));
        var builder = new StringBuilder(visibleBytes * 3 + 16);
        for (var index = 0; index < visibleBytes; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (bytes.Length > visibleBytes)
        {
            builder.Append(" ...");
        }

        return builder.ToString();
    }

    public static string ByteHex(byte value)
    {
        return value.ToString("X2", CultureInfo.InvariantCulture);
    }
}
