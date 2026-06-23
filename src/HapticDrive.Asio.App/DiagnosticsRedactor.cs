using System.Text.RegularExpressions;
using HapticDrive.Asio.Core.Diagnostics;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Asio.App;

internal sealed partial class SupportBundleDiagnosticRedactor : IDiagnosticRedactor
{
    private readonly DiagnosticRedactionMode _mode;

    public SupportBundleDiagnosticRedactor(DiagnosticRedactionMode mode)
    {
        _mode = mode;
    }

    public string RedactText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = value.Trim();
        redacted = redacted.Replace(PHprControlledWriteApproval.Phrase, "<redacted-approval>", StringComparison.Ordinal);
        redacted = UserProfilePathRegex().Replace(
            redacted,
            match => _mode == DiagnosticRedactionMode.Safe
                ? "%USERPROFILE%\\<redacted-path>"
                : $"%USERPROFILE%{match.Groups[2].Value}");
        redacted = AbsoluteWindowsPathRegex().Replace(
            redacted,
            match => _mode == DiagnosticRedactionMode.Safe
                ? "<redacted-path>"
                : $"{match.Groups[1].Value}<redacted-path>");
        redacted = UncPathRegex().Replace(redacted, @"\\<redacted-host>\<redacted-path>");
        redacted = HidPathRegex().Replace(redacted, "$1<redacted>");
        redacted = RawUsbPayloadRegex().Replace(redacted, "$1<redacted>");
        redacted = RawProtocolBytesRegex().Replace(redacted, "$1<redacted>");
        redacted = SerialLabelRegex().Replace(redacted, "$1<redacted>");
        redacted = LongSerialTokenRegex().Replace(redacted, "<redacted>");
        redacted = ProcessIdRegex().Replace(redacted, "$1<redacted>");
        redacted = HostNameRegex().Replace(redacted, "$1<redacted-host>");
        redacted = Ipv4MappedIpv6Regex().Replace(redacted, "<private-ip>");
        redacted = Ipv6PrivateRegex().Replace(redacted, "<private-ip>");

        if (_mode == DiagnosticRedactionMode.Safe)
        {
            redacted = PrivateIpRegex().Replace(redacted, "<private-ip>");
        }

        return redacted;
    }

    public IReadOnlyDictionary<string, string> RedactProperties(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.ToDictionary(
            pair => pair.Key,
            pair => RedactText(pair.Value),
            StringComparer.Ordinal);
    }

    public static IReadOnlyList<string> GetAppliedRedactionCategories(DiagnosticRedactionMode mode)
    {
        var categories = new List<string>
        {
            "approval-phrase",
            "user-profile-paths",
            "absolute-paths",
            "unc-paths",
            "hid-paths",
            "raw-usb-payloads",
            "raw-protocol-bytes",
            "serials",
            "process-ids",
            "hostnames",
            "ipv6-private-and-loopback",
            "ipv4-mapped-ipv6"
        };

        if (mode == DiagnosticRedactionMode.Safe)
        {
            categories.Add("private-ipv4");
        }

        return categories;
    }

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)[^\\]+(\\[^\r\n;,""]*)?")]
    private static partial Regex UserProfilePathRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\)(?!Users\\<redacted>|<redacted-path>)[^\r\n;,""]+")]
    private static partial Regex AbsoluteWindowsPathRegex();

    [GeneratedRegex(@"\\\\[^\s\\/:*?""<>|]+\\[^\s;,""]+")]
    private static partial Regex UncPathRegex();

    [GeneratedRegex(@"(?i)((?:\\\\\?\\)?(?:hid|usb)[#\\][^#\\\s]*(?:vid_[0-9a-f]{4}|pid_[0-9a-f]{4})[^#\\\s]*[#\\])[^#\\\s]+")]
    private static partial Regex HidPathRegex();

    [GeneratedRegex(@"(?i)\b((?:raw usb|raw hid|usb payload|raw bytes?|payload bytes?|transfer bytes?)\s*[:=]\s*)(?:[0-9a-f]{2}\s+){2,}[0-9a-f]{2}\b")]
    private static partial Regex RawUsbPayloadRegex();

    [GeneratedRegex(@"(?i)\b((?:raw protocol bytes?|protocol bytes?)\s*[:=]\s*)(?:[0-9a-f]{2}\s+){2,}[0-9a-f]{2}\b")]
    private static partial Regex RawProtocolBytesRegex();

    [GeneratedRegex(@"(?i)\b(serial|sn|s/n)\s*[:=_-]?\s*[a-z0-9][a-z0-9_-]{5,}\b")]
    private static partial Regex SerialLabelRegex();

    [GeneratedRegex(@"(?i)\b(?=[a-z0-9_-]{10,}\b)(?=.*[a-z])(?=.*[0-9])[a-z0-9_-]{10,}\b")]
    private static partial Regex LongSerialTokenRegex();

    [GeneratedRegex(@"(?i)\b(pid|process id)\s*[:=#]?\s*\d+\b")]
    private static partial Regex ProcessIdRegex();

    [GeneratedRegex(@"(?i)\b(hostname|host)\s*[:=]\s*[a-z0-9][a-z0-9.-]*\b")]
    private static partial Regex HostNameRegex();

    [GeneratedRegex(@"(?i)::ffff:(?:10\.\d{1,3}\.\d{1,3}\.\d{1,3}|127\.\d{1,3}\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[0-1])\.\d{1,3}\.\d{1,3}|169\.254\.\d{1,3}\.\d{1,3})")]
    private static partial Regex Ipv4MappedIpv6Regex();

    [GeneratedRegex(@"(?i)(?<![0-9a-f:])::1(?![0-9a-f:])|(?<![0-9a-f:])(?:fd[0-9a-f]{2}|fc[0-9a-f]{2})[:0-9a-f%]+|(?<![0-9a-f:])fe80[:0-9a-f%]+")]
    private static partial Regex Ipv6PrivateRegex();

    [GeneratedRegex(@"\b(?:(?:10|127)\.\d{1,3}\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[0-1])\.\d{1,3}\.\d{1,3}|169\.254\.\d{1,3}\.\d{1,3})\b")]
    private static partial Regex PrivateIpRegex();
}
