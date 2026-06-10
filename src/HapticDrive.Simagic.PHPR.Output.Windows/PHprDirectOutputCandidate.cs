using System.Globalization;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprDirectOutputCandidate
{
    public const byte F1EcFeatureReportId = SimHubF1EcRealReportEncoder.Prefix0;

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

    public bool HasF1EcFeatureReportId => FeatureReportIds.Contains(F1EcFeatureReportId);

    public PHprHidReportTransport PreferredTransport
    {
        get
        {
            if (FeatureReportByteLength == SimHubF1EcRealReportEncoder.PayloadLengthBytes
                && HasF1EcFeatureReportId)
            {
                return PHprHidReportTransport.FeatureReport;
            }

            if (OutputReportByteLength == SimHubF1EcRealReportEncoder.PayloadLengthBytes)
            {
                return PHprHidReportTransport.OutputReport;
            }

            if (FeatureReportByteLength == SimHubF1EcRealReportEncoder.PayloadLengthBytes)
            {
                return PHprHidReportTransport.FeatureReport;
            }

            return PHprHidReportTransport.OutputReport;
        }
    }

    public string PreferredTransportText =>
        $"{PreferredTransport}; report ID {FormatSelectedReportId(GetPreferredReportId(PreferredTransport))}";

    public string SelectedReportLengthSource => GetReportLengthSource(PreferredTransport);

    public string SafeLabel =>
        $"{VendorProductText}; {SafeDisplayName}; source {SourceMethod}; raw-input-only {IsRawInputOnly}; openable HID path {HasOpenableHidPath}; class {FormatValue(DeviceClass)}; interface {FormatValue(InterfaceNumber)}; collection {FormatValue(CollectionNumber)}; {UsageText}; reports {ReportLengthText}; report IDs {ReportIdText}; preferred transport {PreferredTransportText}; confidence {Confidence}; {ConfidenceReason}";

    public PHprHidDeviceSelector ToSelector(
        byte? reportId = null,
        PHprHidReportTransport? transport = null)
    {
        if (!HasOpenableHidPath)
        {
            return PHprHidDeviceSelector.None;
        }

        var selectedTransport = transport ?? PreferredTransport;
        var reportLength = GetReportLength(selectedTransport);
        var selectedReportId = reportId ?? GetPreferredReportId(selectedTransport);

        return new PHprHidDeviceSelector(
            DevicePath,
            SafeDisplayName,
            BuildInterfaceName(),
            selectedReportId,
            reportLength,
            selectedTransport).Normalize();
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
        var featureLengthMatches = FeatureReportByteLength == SimHubF1EcRealReportEncoder.PayloadLengthBytes;

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

        if (HasF1EcFeatureReportId)
        {
            reasons.Add("feature report ID 0xF1 is available and likely matches the F1 EC command family");
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

        if (featureLengthMatches)
        {
            confidence = HasOpenableHidPath
                ? isSimagicFamily
                    ? PHprDirectOutputCandidateConfidence.Preferred
                    : PHprDirectOutputCandidateConfidence.LikelyOutputCapable
                : confidence;
            reasons.Add($"feature report length matches {SimHubF1EcRealReportEncoder.PayloadLengthBytes} bytes");
        }

        if (OutputReportByteLength is null)
        {
            reasons.Add("output report length unavailable; no role guessed from path alone");
        }
        else if (!outputLengthMatches)
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

        parts.Add($"transport: {PreferredTransport}");
        parts.Add($"report length source: {SelectedReportLengthSource}");
        parts.Add(UsageText);
        return string.Join("; ", parts);
    }

    private int GetReportLength(PHprHidReportTransport transport)
    {
        return transport == PHprHidReportTransport.FeatureReport
            ? FeatureReportByteLength is > 0
                ? FeatureReportByteLength.Value
                : SimHubF1EcRealReportEncoder.PayloadLengthBytes
            : OutputReportByteLength is > 0
                ? OutputReportByteLength.Value
                : SimHubF1EcRealReportEncoder.PayloadLengthBytes;
    }

    private string GetReportLengthSource(PHprHidReportTransport transport)
    {
        if (transport == PHprHidReportTransport.FeatureReport)
        {
            return FeatureReportByteLength is > 0
                ? "device feature-report metadata"
                : "SimHub F1 EC hypothesis default; device feature-report length was not available";
        }

        return OutputReportByteLength is > 0
            ? "device output-report metadata"
            : "SimHub F1 EC hypothesis default; device output-report length was not available";
    }

    private byte? GetPreferredReportId(PHprHidReportTransport transport)
    {
        var reportIds = transport == PHprHidReportTransport.FeatureReport
            ? FeatureReportIds
            : OutputReportIds;

        if (transport == PHprHidReportTransport.FeatureReport
            && reportIds.Contains(F1EcFeatureReportId))
        {
            return F1EcFeatureReportId;
        }

        if (reportIds.Count == 1)
        {
            return reportIds[0] == 0 ? null : reportIds[0];
        }

        return null;
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

    private static string FormatSelectedReportId(byte? reportId)
    {
        return reportId is null ? "none" : $"0x{reportId.Value:X2}";
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
