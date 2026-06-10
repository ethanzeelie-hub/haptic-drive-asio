using System.Globalization;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectOutputCandidate
{
    public string CandidateId { get; init; } = "local-hid:unknown";

    public string DevicePath { get; init; } = string.Empty;

    public string DisplayName { get; init; } = "Local HID device";

    public string DeviceClass { get; init; } = "HID";

    public PHprDirectOutputCandidateSourceMethod SourceMethod { get; init; } = PHprDirectOutputCandidateSourceMethod.Unknown;

    public ushort? VendorId { get; init; }

    public ushort? ProductId { get; init; }

    public string? InterfaceNumber { get; init; }

    public string? CollectionNumber { get; init; }

    public ushort? HidUsagePage { get; init; }

    public ushort? HidUsage { get; init; }

    public int? InputReportByteLength { get; init; }

    public int? OutputReportByteLength { get; init; }

    public int? FeatureReportByteLength { get; init; }

    public IReadOnlyList<byte> InputReportIds { get; init; } = [];

    public IReadOnlyList<byte> OutputReportIds { get; init; } = [];

    public IReadOnlyList<byte> FeatureReportIds { get; init; } = [];

    public PHprDirectOutputCandidateConfidence Confidence { get; init; } = PHprDirectOutputCandidateConfidence.Unknown;

    public string ConfidenceReason { get; init; } = "No output-capable Simagic signal was inferred.";

    public bool IsSelected => HasOpenableHidPath && !string.IsNullOrWhiteSpace(DevicePath);

    public bool IsRawInputOnly => SourceMethod == PHprDirectOutputCandidateSourceMethod.RawInputMetadata;

    public bool HasOpenableHidPath => SourceMethod == PHprDirectOutputCandidateSourceMethod.HidDeviceInterface
        && PHprHidPathSafety.IsAbsoluteWindowsDevicePath(DevicePath);

    public string VendorProductText => VendorId is null || ProductId is null
        ? "VID/PID unavailable"
        : $"VID_{VendorId:X4}/PID_{ProductId:X4}";

    public string SafeDisplayName => SanitizeDisplayName(DisplayName);

    public string ReportLengthText =>
        $"input {FormatReportLength(InputReportByteLength)}, output {FormatReportLength(OutputReportByteLength)}, feature {FormatReportLength(FeatureReportByteLength)}";

    public string ReportIdText =>
        $"input IDs {FormatReportIds(InputReportIds)}, output IDs {FormatReportIds(OutputReportIds)}, feature IDs {FormatReportIds(FeatureReportIds)}";

    public string UsageText => HidUsagePage is null || HidUsage is null
        ? "usage unavailable"
        : $"usage 0x{HidUsagePage.Value:X4}/0x{HidUsage.Value:X4}";

    public bool HasKnownOutputReportCapability => OutputReportByteLength is > 0;

    public bool HasKnownFeatureReportCapability => FeatureReportByteLength is > 0;

    public bool HasOutputOrFeatureReportCapability =>
        HasKnownOutputReportCapability || HasKnownFeatureReportCapability;

    public string SelectedReportLengthSource => OutputReportByteLength is > 0
        ? "device output-report metadata"
        : "SimHub F1 EC hypothesis default; device output-report length was not available";

    public string SafeLabel =>
        $"{VendorProductText}; {SafeDisplayName}; source {SourceMethod}; raw-input-only {IsRawInputOnly}; openable HID path {HasOpenableHidPath}; class {FormatValue(DeviceClass)}; interface {FormatValue(InterfaceNumber)}; collection {FormatValue(CollectionNumber)}; {UsageText}; reports {ReportLengthText}; report IDs {ReportIdText}; confidence {Confidence}; {ConfidenceReason}";

    public PHprHidDeviceSelector ToSelector(byte? reportId = null)
    {
        if (!HasOpenableHidPath)
        {
            return PHprHidDeviceSelector.None;
        }

        var reportLength = OutputReportByteLength is > 0
            ? OutputReportByteLength.Value
            : SimHubF1EcRealReportEncoder.PayloadLengthBytes;

        return new PHprHidDeviceSelector(
            DevicePath,
            SafeDisplayName,
            BuildInterfaceName(),
            reportId,
            reportLength).Normalize();
    }

    public override string ToString()
    {
        return SafeLabel;
    }

    public PHprDirectOutputCandidate Score()
    {
        var isSimagicFamily = SimagicPhprDeviceIdentity.IsSimagicFamilyVendor(VendorId);
        var isObservedFamilyProduct = SimagicPhprDeviceIdentity.IsObservedSimagicFamilyProduct(ProductId);
        var outputLengthMatches = OutputReportByteLength == SimHubF1EcRealReportEncoder.PayloadLengthBytes;

        var confidence = HasOpenableHidPath
            ? PHprDirectOutputCandidateConfidence.GenericHid
            : PHprDirectOutputCandidateConfidence.Unknown;
        var reasons = new List<string> { "HID candidate" };

        if (IsRawInputOnly)
        {
            reasons.Add("Raw Input metadata only; not openable by the HID writer");
        }

        if (HasOpenableHidPath)
        {
            reasons.Add("HID device-interface path");
        }

        if (isSimagicFamily)
        {
            confidence = PHprDirectOutputCandidateConfidence.SimagicFamily;
            reasons.Add("VID_3670 Simagic-family vendor");
        }

        if (isObservedFamilyProduct)
        {
            reasons.Add($"observed family PID_{ProductId:X4}");
        }

        if (OutputReportByteLength is > 0)
        {
            reasons.Add($"output report capability {OutputReportByteLength.Value.ToString(CultureInfo.InvariantCulture)} bytes");
        }

        if (FeatureReportByteLength is > 0)
        {
            reasons.Add($"feature report capability {FeatureReportByteLength.Value.ToString(CultureInfo.InvariantCulture)} bytes");
        }

        if (outputLengthMatches)
        {
            confidence = HasOpenableHidPath
                ? isSimagicFamily
                    ? PHprDirectOutputCandidateConfidence.Preferred
                    : PHprDirectOutputCandidateConfidence.LikelyOutputCapable
                : confidence;
            reasons.Add($"output report length matches {SimHubF1EcRealReportEncoder.PayloadLengthBytes} bytes");
        }
        else if (OutputReportByteLength is null)
        {
            reasons.Add("output report length unavailable; no role guessed from path alone");
        }
        else
        {
            reasons.Add($"output report length {OutputReportByteLength.Value.ToString(CultureInfo.InvariantCulture)} bytes does not match current P-HPR hypothesis");
        }

        return this with
        {
            Confidence = confidence,
            ConfidenceReason = string.Join("; ", reasons.Distinct(StringComparer.OrdinalIgnoreCase))
        };
    }

    private string BuildInterfaceName()
    {
        var parts = new List<string> { VendorProductText };
        if (!string.IsNullOrWhiteSpace(InterfaceNumber))
        {
            parts.Add($"MI_{InterfaceNumber}");
        }

        if (!string.IsNullOrWhiteSpace(CollectionNumber))
        {
            parts.Add($"COL{CollectionNumber}");
        }

        parts.Add($"report length source: {SelectedReportLengthSource}");
        parts.Add(UsageText);
        return string.Join("; ", parts);
    }

    private static string FormatReportLength(int? value)
    {
        return value is > 0 ? $"{value.Value.ToString(CultureInfo.InvariantCulture)} bytes" : "unavailable";
    }

    private static string FormatReportIds(IReadOnlyList<byte>? reportIds)
    {
        if (reportIds is not { Count: > 0 })
        {
            return "unavailable";
        }

        return string.Join(
            ",",
            reportIds
                .Distinct()
                .Order()
                .Select(reportId => reportId == 0
                    ? "none"
                    : $"0x{reportId:X2}"));
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unavailable" : SanitizeDisplayName(value);
    }

    private static string SanitizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Local HID device";
        }

        var trimmed = value.Trim();
        if (trimmed.Contains(@"\\?\", StringComparison.Ordinal)
            || trimmed.Contains(@"\\.\", StringComparison.Ordinal)
            || (trimmed.Contains('#', StringComparison.Ordinal)
                && trimmed.Contains("VID_", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains("PID_", StringComparison.OrdinalIgnoreCase)))
        {
            return "Local HID device";
        }

        return trimmed;
    }
}
