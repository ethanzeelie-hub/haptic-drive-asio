namespace HapticDrive.Simagic.PHPR.Output.Windows;

public sealed record PHprHidReportShapeValidationResult(
    bool Attempted,
    bool Succeeded,
    bool Failed,
    string Message,
    string? SanitizedErrorCategory,
    PHprHidReportTransport? Transport = null,
    byte? ReportId = null,
    int? ReportLength = null,
    string? ExpectedFirstBytes = null)
{
    public static PHprHidReportShapeValidationResult NotAttempted { get; } = new(
        Attempted: false,
        Succeeded: false,
        Failed: false,
        "No P-HPR HID report-shape validation has been attempted.",
        SanitizedErrorCategory: null);

    public static string ExpectedF1EcStartFirstBytes { get; } = FormatExpectedFirstBytes();

    public static PHprHidReportShapeValidationResult Success(
        string message,
        PHprHidDeviceSelector selector)
    {
        return new PHprHidReportShapeValidationResult(
            Attempted: true,
            Succeeded: true,
            Failed: false,
            message,
            SanitizedErrorCategory: null,
            selector.Transport,
            selector.ReportId,
            selector.ReportLength,
            ExpectedF1EcStartFirstBytes);
    }

    public static PHprHidReportShapeValidationResult Failure(
        string message,
        string sanitizedErrorCategory,
        PHprHidDeviceSelector? selector = null)
    {
        return new PHprHidReportShapeValidationResult(
            Attempted: true,
            Succeeded: false,
            Failed: true,
            message,
            sanitizedErrorCategory,
            selector?.Transport,
            selector?.ReportId,
            selector?.ReportLength,
            selector is null ? null : ExpectedF1EcStartFirstBytes);
    }

    private static string FormatExpectedFirstBytes()
    {
        return string.Join(
            " ",
            new[]
            {
                SimHubF1EcRealReportEncoder.Prefix0,
                SimHubF1EcRealReportEncoder.Prefix1,
                SimHubF1EcRealReportEncoder.BrakeModuleByte,
                SimHubF1EcRealReportEncoder.StartStateByte,
                (byte)50,
                (byte)10,
                (byte)0
            }.Select(value => value.ToString("X2")));
    }
}

public static class PHprHidReportShapeValidator
{
    public static PHprHidReportShapeValidationResult Validate(
        PHprDirectOutputCandidate? candidate,
        PHprHidDeviceSelector selector)
    {
        if (candidate is null)
        {
            return PHprHidReportShapeValidationResult.NotAttempted;
        }

        var normalizedSelector = (selector ?? PHprHidDeviceSelector.None).Normalize();
        if (candidate.IsRawInputOnly)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "Selected candidate is Raw Input metadata only; no HID report shape can be validated.",
                "RawInputOnly",
                normalizedSelector);
        }

        if (!candidate.HasOpenableHidPath)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "Selected candidate does not expose an openable HID device-interface path for report-shape validation.",
                "NoOpenableHidPath",
                normalizedSelector);
        }

        if (!normalizedSelector.IsSelected)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "No selected HID device/interface/report is available for report-shape validation.",
                "NotSelected",
                normalizedSelector);
        }

        return normalizedSelector.Transport switch
        {
            PHprHidReportTransport.FeatureReport => ValidateFeatureReport(candidate, normalizedSelector),
            _ => ValidateOutputReport(candidate, normalizedSelector)
        };
    }

    private static PHprHidReportShapeValidationResult ValidateOutputReport(
        PHprDirectOutputCandidate candidate,
        PHprHidDeviceSelector selector)
    {
        if (candidate.OutputReportByteLength is not > 0)
        {
            var message = candidate.FeatureReportByteLength is > 0
                ? "Selected transport is OutputReport, but this candidate exposes feature reports and no HID output-report byte length."
                : "Selected candidate HID output-report byte length is unavailable; open-check alone is not enough to permit a real pulse.";
            return PHprHidReportShapeValidationResult.Failure(message, "OutputReportLengthUnavailable", selector);
        }

        if (selector.ReportLength != candidate.OutputReportByteLength.Value)
        {
            return PHprHidReportShapeValidationResult.Failure(
                $"Selected report length {selector.ReportLength:N0} bytes does not match HID output-report capability {candidate.OutputReportByteLength.Value:N0} bytes.",
                "OutputReportLengthMismatch",
                selector);
        }

        if (selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            return PHprHidReportShapeValidationResult.Failure(
                $"Selected output-report capability is {selector.ReportLength:N0} bytes, but the current P-HPR writer only has a {SimHubF1EcRealReportEncoder.PayloadLengthBytes:N0}-byte SimHub F1 EC hypothesis.",
                "UnsupportedOutputReportLength",
                selector);
        }

        if (candidate.OutputReportIds.Count > 0)
        {
            var selectedReportId = selector.ReportId ?? (byte)0;
            if (!candidate.OutputReportIds.Contains(selectedReportId))
            {
                return PHprHidReportShapeValidationResult.Failure(
                    "Selected report ID is not advertised by the HID output-report capabilities.",
                    "OutputReportIdMismatch",
                    selector);
            }
        }

        return PHprHidReportShapeValidationResult.Success(
            "No-command HID report-shape validation succeeded from output-report capability metadata; no P-HPR report was sent.",
            selector);
    }

    private static PHprHidReportShapeValidationResult ValidateFeatureReport(
        PHprDirectOutputCandidate candidate,
        PHprHidDeviceSelector selector)
    {
        if (candidate.FeatureReportByteLength is not > 0)
        {
            return PHprHidReportShapeValidationResult.Failure(
                "Selected transport is FeatureReport, but this candidate HID feature-report byte length is unavailable.",
                "FeatureReportLengthUnavailable",
                selector);
        }

        if (selector.ReportLength != candidate.FeatureReportByteLength.Value)
        {
            return PHprHidReportShapeValidationResult.Failure(
                $"Selected report length {selector.ReportLength:N0} bytes does not match HID feature-report capability {candidate.FeatureReportByteLength.Value:N0} bytes.",
                "FeatureReportLengthMismatch",
                selector);
        }

        if (selector.ReportLength != SimHubF1EcRealReportEncoder.PayloadLengthBytes)
        {
            return PHprHidReportShapeValidationResult.Failure(
                $"Selected feature-report capability is {selector.ReportLength:N0} bytes, but the current P-HPR writer only has a {SimHubF1EcRealReportEncoder.PayloadLengthBytes:N0}-byte SimHub F1 EC hypothesis.",
                "UnsupportedFeatureReportLength",
                selector);
        }

        if (candidate.FeatureReportIds.Count > 0)
        {
            var selectedReportId = selector.ReportId ?? (byte)0;
            if (!candidate.FeatureReportIds.Contains(selectedReportId))
            {
                return PHprHidReportShapeValidationResult.Failure(
                    "Selected report ID is not advertised by the HID feature-report capabilities.",
                    "FeatureReportIdMismatch",
                    selector);
            }
        }

        var f1Message = selector.ReportId == PHprDirectOutputCandidate.F1EcFeatureReportId
            ? " Feature report ID 0xF1 is selected and matches the known F1 EC command family prefix."
            : string.Empty;
        return PHprHidReportShapeValidationResult.Success(
            $"No-command HID report-shape validation succeeded from feature-report capability metadata; no P-HPR report was sent.{f1Message}",
            selector);
    }
}
