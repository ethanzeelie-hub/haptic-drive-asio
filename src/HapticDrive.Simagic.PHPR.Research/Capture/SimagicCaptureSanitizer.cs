using System.Text.RegularExpressions;

namespace HapticDrive.Simagic.PHPR.Research.Capture;

public static partial class SimagicCaptureSanitizer
{
    public static SimagicCaptureMetadata Sanitize(SimagicCaptureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var changed = ContainsSensitiveData(metadata);
        var rawCaptureFileName = string.IsNullOrWhiteSpace(metadata.RawCapturePath)
            ? null
            : SanitizeFileNameOnly(metadata.RawCapturePath);

        return metadata with
        {
            CaptureId = SanitizeText(metadata.CaptureId) ?? "capture-redacted",
            ScenarioName = SanitizeText(metadata.ScenarioName),
            CaptureFileName = SanitizeFileNameOnly(metadata.CaptureFileName),
            Software = Sanitize(metadata.Software),
            Device = Sanitize(metadata.Device),
            Action = Sanitize(metadata.Action),
            Notes = SanitizeText(metadata.Notes),
            RedactionStatus = changed || metadata.RedactionStatus == SimagicCaptureRedactionStatus.ContainsPrivateData
                ? SimagicCaptureRedactionStatus.Redacted
                : metadata.RedactionStatus,
            ContainsSerialNumbers = false,
            ContainsPrivatePaths = false,
            RawCapturePath = rawCaptureFileName,
            SanitizedSummaryPath = SanitizeRelativePath(metadata.SanitizedSummaryPath)
        };
    }

    public static string? SanitizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = UserProfileRegex().Replace(value.Trim(), "$1<redacted>");
        sanitized = PrivateCapturePathRegex().Replace(sanitized, "captures/private/<redacted>");
        sanitized = RawBytesRegex().Replace(sanitized, "$1: <redacted>");
        sanitized = UsbPathSerialRegex().Replace(sanitized, "$1<redacted>");
        sanitized = SerialLabelRegex().Replace(sanitized, "$1<redacted>");
        sanitized = LongSerialTokenRegex().Replace(sanitized, "<redacted>");
        return sanitized;
    }

    public static bool ContainsSensitiveData(SimagicCaptureMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.ContainsSerialNumbers
            || metadata.ContainsPrivatePaths
            || metadata.RedactionStatus == SimagicCaptureRedactionStatus.ContainsPrivateData)
        {
            return true;
        }

        var values = new[]
        {
            metadata.CaptureId,
            metadata.ScenarioName,
            metadata.CaptureFileName,
            metadata.Software.CaptureTool,
            metadata.Software.CaptureToolVersion,
            metadata.Software.SoftwareUnderTest,
            metadata.Software.SoftwareUnderTestVersion,
            metadata.Software.SimProVersion,
            metadata.Software.SimHubVersion,
            metadata.Device.P700FirmwareVersion,
            metadata.Device.DeviceInventoryReference,
            metadata.Action.ActionPerformed,
            metadata.Action.ActualObservedBehaviour,
            metadata.Notes,
            metadata.RawCapturePath,
            metadata.SanitizedSummaryPath
        };

        return values.Any(ContainsSensitiveData);
    }

    public static bool ContainsSensitiveData(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return UserProfileRegex().IsMatch(value)
            || PrivateAbsolutePathRegex().IsMatch(value)
            || SerialLabelRegex().IsMatch(value)
            || UsbPathSerialRegex().IsMatch(value)
            || LongSerialTokenRegex().IsMatch(RemoveKnownSafeTokens(value));
    }

    private static SimagicCaptureSoftwareContext Sanitize(SimagicCaptureSoftwareContext context)
    {
        return context with
        {
            CaptureTool = SanitizeText(context.CaptureTool),
            CaptureToolVersion = SanitizeText(context.CaptureToolVersion),
            SoftwareUnderTest = SanitizeText(context.SoftwareUnderTest),
            SoftwareUnderTestVersion = SanitizeText(context.SoftwareUnderTestVersion),
            SimProVersion = SanitizeText(context.SimProVersion),
            SimHubVersion = SanitizeText(context.SimHubVersion)
        };
    }

    private static SimagicCaptureDeviceContext Sanitize(SimagicCaptureDeviceContext context)
    {
        return context with
        {
            P700FirmwareVersion = SanitizeText(context.P700FirmwareVersion),
            DeviceInventoryReference = SanitizeText(context.DeviceInventoryReference)
        };
    }

    private static SimagicCaptureActionContext Sanitize(SimagicCaptureActionContext context)
    {
        return context with
        {
            ActionPerformed = SanitizeText(context.ActionPerformed),
            ActualObservedBehaviour = SanitizeText(context.ActualObservedBehaviour)
        };
    }

    private static string? SanitizeFileNameOnly(string? pathOrFileName)
    {
        var sanitized = SanitizeText(pathOrFileName);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        var fileName = Path.GetFileName(sanitized);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = sanitized.Replace('\\', '_').Replace('/', '_');
        }

        return SimagicCaptureFilenameBuilder.Slugify(Path.GetFileNameWithoutExtension(fileName))
            + Path.GetExtension(fileName).ToLowerInvariant();
    }

    private static string? SanitizeRelativePath(string? value)
    {
        var sanitized = SanitizeText(value);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        return sanitized.Replace('\\', '/');
    }

    private static string RemoveKnownSafeTokens(string value)
    {
        return KnownSafeTokenRegex().Replace(value, "");
    }

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)[^\\]+")]
    private static partial Regex UserProfileRegex();

    [GeneratedRegex(@"(?i)\b[A-Z]:\\(?!Users\\<redacted>)[^\r\n]*")]
    private static partial Regex PrivateAbsolutePathRegex();

    [GeneratedRegex(@"(?i)(captures[/\\]private[/\\])[^""'\s]+")]
    private static partial Regex PrivateCapturePathRegex();

    [GeneratedRegex(@"(?i)\b(raw bytes?|payload bytes?|transfer bytes?)\s*[:=]\s*(?:[0-9a-f]{2}\s+){2,}[0-9a-f]{2}\b")]
    private static partial Regex RawBytesRegex();

    [GeneratedRegex(@"(?i)((?:hid|usb)[#\\][^#\\\s]*(?:vid_[0-9a-f]{4}|pid_[0-9a-f]{4})[^#\\\s]*[#\\])[^#\\\s]+")]
    private static partial Regex UsbPathSerialRegex();

    [GeneratedRegex(@"(?i)\b(serial|sn|s/n)\s*[:=_-]?\s*[a-z0-9][a-z0-9_-]{5,}\b")]
    private static partial Regex SerialLabelRegex();

    [GeneratedRegex(@"(?i)\b(?=[a-z0-9]{10,}\b)(?=.*[a-z])(?=.*[0-9])[a-z0-9]{10,}\b")]
    private static partial Regex LongSerialTokenRegex();

    [GeneratedRegex(@"(?i)\b(vid_[0-9a-f]{4}|pid_[0-9a-f]{4}|mi_[0-9a-f]{2}|col[0-9a-f]{2}|p700|p-hpr|simagic|simpro|simhub|usbpcap|wireshark|brake|throttle|both|unknown)\b")]
    private static partial Regex KnownSafeTokenRegex();
}
